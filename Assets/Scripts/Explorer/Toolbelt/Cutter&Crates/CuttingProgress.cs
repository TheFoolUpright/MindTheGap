using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CutPath))]
public class CuttingProgress : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private CutterTool cutter;

    [Header("Movement Settings")]
    [SerializeField]
    private float directionThreshold = 0.015f;

    [SerializeField]
    private float resumeTolerance = 0.02f;

    [SerializeField]
    private float maxStepDistance = 0.06f;

    [Header("Runtime State - DO NOT EDIT During Play")]
    [SerializeField] private float cutLength;
    [SerializeField] private bool completed;

    [Header("Events")]
    [SerializeField] private UnityEvent onCutCompleted = new UnityEvent(); 

    private CutPath path;
    private bool started;
    private bool tracking;

    private float startDistance;
    private float previousDistance;

    // 0 = not chosen, 1 = forward, -1 = backward.
    private int direction;

    // They allow the UI to read the state of the cut without changing it.
    public bool HasStarted
    {
        get { return started; }
    }

    public float StartDistance
    {
        get { return startDistance; }
    }

    public int Direction
    {
        get { return direction; }
    }
    // End of the UI reading section ------------------

    // Returns the fraction of the path that has been cut, from 0 to 1.
    public float Progress
    {
        get
        {
            if (path == null)
                return 0f;

            float length = path.GetLength();

            if (length <= 0.00001f)
                return 0f;

            return Mathf.Clamp01(cutLength / length);
        }
    }

    public bool IsCompleted
    {
        get { return completed; }
    }

    private void Awake()
    {
        path = GetComponent<CutPath>();
    }

    private void OnDisable()
    {
        // Keep progress, but require contact with each other to be established again.
        tracking = false;
    }

    private void Update()
    {
        if (completed)
            return;

        if (cutter == null || !cutter.IsUseRequested)
        {
            tracking = false;
            return;
        }

        Vector3 nearestPoint; //...to the cutter tip.
        float distance; //...along the path perimeter to the nearest point.

        bool hasContact = path.TryGetContact(
            cutter.CuttingTip.position,
            out nearestPoint,
            out distance
        );

        if (!hasContact)
        {
            tracking = false;
            return;
        }

        float length = path.GetLength();

        if (length <= 0.00001f)
            return;

        // The first valid contact defines the starting point.
        if (!started)
        {
            started = true;
            tracking = true;

            startDistance = distance;
            previousDistance = distance;
            return;
        }

        float frontier = startDistance + direction * cutLength;
        float offsetFromFrontier = GetSignedDistance(frontier, distance, length);

        if (!tracking)
        {
            // Resume near the end of the existing cut.
            if (Mathf.Abs(offsetFromFrontier) > resumeTolerance)
                return;

            // Approach from the already-cut side to avoid skipping ahead.
            if (direction != 0 && offsetFromFrontier * direction > 0f)
                return;

            previousDistance = distance;
            tracking = true;
            return;
        }

        float step = GetSignedDistance(
            previousDistance,
            distance,
            length
        );

        previousDistance = distance;

        // A large jump must not count as cutting the missing section.
        if (Mathf.Abs(step) > maxStepDistance)
        {
            tracking = false;
            return;
        }

        if (direction == 0)
        {
            // Ignore small hand movements before choosing a direction.
            if (Mathf.Abs(offsetFromFrontier) < directionThreshold)
                return;

            if (Mathf.Abs(offsetFromFrontier) > maxStepDistance)
            {
                tracking = false;
                return;
            }

            if (offsetFromFrontier > 0f)
                direction = 1;
            else
                direction = -1;
        }

        float advance = offsetFromFrontier * direction;

        if (advance <= 0f)
        {
            // Moving backward never removes or adds progress.
            if (-advance > resumeTolerance)
                tracking = false;

            return;
        }

        if (advance > maxStepDistance)
        {
            tracking = false;
            return;
        }

        cutLength = Mathf.Min(cutLength + advance, length);

        if (cutLength >= length)
        {
            completed = true;
            tracking = false;

            onCutCompleted.Invoke();
        }
    }

    // Returns the shortest signed distance around the closed perimeter.
    private float GetSignedDistance(float from, float to, float length)
    {
        return Mathf.Repeat(
            to - from + length * 0.5f,
            length
        ) - length * 0.5f;
    }

    private void OnDrawGizmos()
    {
        if (!started)
            return;

        CutPath currentPath = GetComponent<CutPath>();

        if (currentPath == null)
            return;

        // Show the position where cutting must continue.
        Vector3 frontierPosition = currentPath.GetPointAtDistance(
            startDistance + direction * cutLength
        );

        if (completed)
            Gizmos.color = Color.green;
        else
            Gizmos.color = Color.cyan;

        Gizmos.DrawSphere(frontierPosition, 0.012f);
    }
}