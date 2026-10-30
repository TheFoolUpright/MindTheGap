using UnityEngine;

public class CutPath : MonoBehaviour
{
    [Header("Path Size")]
    [SerializeField] private float width = 0.60f;
    [SerializeField] private float height = 0.60f;

    [Header("Contact Tolerance")]
    [SerializeField] private float edgeTolerance = 0.025f;
    [SerializeField] private float depthTolerance = 0.03f;

    [Header("Preview Only")]
    [SerializeField] private CutterTool previewCutter;

    // Returns a corner of the rectangle in world space.
    private Vector3 GetCorner(int index)
    {
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;

        Vector3 localCorner;

        switch (index)
        {
            case 0:
                localCorner = new Vector3(-halfWidth, -halfHeight, 0f);
                break;

            case 1:
                localCorner = new Vector3(halfWidth, -halfHeight, 0f);
                break;

            case 2:
                localCorner = new Vector3(halfWidth, halfHeight, 0f);
                break;

            default:
                localCorner = new Vector3(-halfWidth, halfHeight, 0f);
                break;
        }

        return transform.TransformPoint(localCorner);
    }

    // Finds the nearest path point and checks contact tolerances.
    // Distances and tolerances are measured in world units.
    public bool TryGetContact(Vector3 tipPosition, out Vector3 nearestPoint, out float distanceAlongPath) // "out" retrieves all information from the function, not just a return value.
    {
        nearestPoint = transform.position;
        distanceAlongPath = 0f;

        // Measure the tip's distance from the path plane.
        Vector3 planeNormal = transform.forward;
        Vector3 offsetFromCenter = tipPosition - transform.position;

        float depth = Vector3.Dot(offsetFromCenter, planeNormal);

        // Project the tip onto the path plane.
        Vector3 tipOnPlane = tipPosition - planeNormal * depth;

        float closestDistanceSquared = float.PositiveInfinity;
        float completedSidesLength = 0f;

        for (int side = 0; side < 4; side++)
        {
            Vector3 start = GetCorner(side);
            Vector3 end = GetCorner((side + 1) % 4);

            float sideLength = Vector3.Distance(start, end);

            // Avoid division by zero if a side has no length.
            if (sideLength <= 0.00001f)
                return false;

            float positionOnSide;

            Vector3 pointOnSide = GetClosestPointOnSide(
                tipOnPlane,
                start,
                end,
                out positionOnSide
            );

            float distanceSquared =
                (tipOnPlane - pointOnSide).sqrMagnitude;

            if (distanceSquared < closestDistanceSquared)
            {
                closestDistanceSquared = distanceSquared;
                nearestPoint = pointOnSide;

                distanceAlongPath =
                    completedSidesLength + sideLength * positionOnSide;
            }

            completedSidesLength += sideLength;
        }

        bool closeToPlane = Mathf.Abs(depth) <= depthTolerance;

        bool closeToEdge =
            closestDistanceSquared <= edgeTolerance * edgeTolerance;

        return closeToPlane && closeToEdge;
    }

    // Returns the closest point on a finite side.
    // positionOnSide: 0 = start, 0.5 = middle, 1 = end.
    private Vector3 GetClosestPointOnSide(
        Vector3 point,
        Vector3 start,
        Vector3 end,
        out float positionOnSide)
    {
        Vector3 sideDirection = end - start;
        Vector3 offsetFromStart = point - start;

        positionOnSide =
            Vector3.Dot(offsetFromStart, sideDirection)
            / sideDirection.sqrMagnitude;

        positionOnSide = Mathf.Clamp01(positionOnSide);

        return start + sideDirection * positionOnSide;
    }

    // Draws an editor preview. This does not render the seam in the game.
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;

        for (int side = 0; side < 4; side++)
        {
            Vector3 start = GetCorner(side);
            Vector3 end = GetCorner((side + 1) % 4);

            Gizmos.DrawLine(start, end);
        }

        if (previewCutter == null || previewCutter.CuttingTip == null)
            return;

        Vector3 tipPosition = previewCutter.CuttingTip.position;

        Vector3 nearestPoint;
        float distanceAlongPath;

        bool hasContact = TryGetContact(
            tipPosition,
            out nearestPoint,
            out distanceAlongPath
        );

        if (hasContact)
        {
            Gizmos.color = Color.green;
        }
        else
        {
            Gizmos.color = Color.red;
        }

        Gizmos.DrawLine(tipPosition, nearestPoint);
        Gizmos.DrawSphere(nearestPoint, 0.01f);
    }
}