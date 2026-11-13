using UnityEngine;

// Put this on the MazeExit object at the doorway between the base tunnel and
// the maze. Its local +Z (blue arrow) must point INTO the maze.
//
// It watches the Explorer's head instead of using OnTriggerEnter, so it works
// with XR teleport/continuous movement without needing a Rigidbody or collider
// on the player. A crossing counts when the head goes from one side of the
// box to the other while it is inside the passage zone.
[DisallowMultipleComponent]
public class TunnelPassage : MonoBehaviour
{
    private enum Side { Unknown, Base, Maze }

    [Tooltip("The XR camera. Falls back to Camera.main.")]
    [SerializeField] private Transform explorerHead;

    [Tooltip("Falls back to ExpeditionController.Instance.")]
    [SerializeField] private ExpeditionController expedition;

    [Header("Passage Zone")]
    [Tooltip("Size of the doorway in local space. X = width, Z = depth of the " +
             "crossing band. Taken from the BoxCollider if there is one.")]
    [SerializeField] private Vector3 passageSize = new Vector3(2f, 3f, 0.5f);

    [Tooltip("How far either side of the band the head is still tracked.")]
    [SerializeField, Min(0.1f)] private float approachDistance = 2f;

    [Tooltip("Extra width allowance so leaning against the tunnel wall still counts.")]
    [SerializeField, Min(0f)] private float widthMargin = 0.3f;

    [Header("Visuals")]
    [SerializeField] private bool hideRendererInPlayMode = true;

    private Side lastSide = Side.Unknown;
    private BoxCollider box;

    private void Awake()
    {
        box = GetComponent<BoxCollider>();

        if (box != null)
        {
            // Never block the player: the cube only marks the doorway.
            box.isTrigger = true;
        }

        if (hideRendererInPlayMode &&
            TryGetComponent(out Renderer meshRenderer))
        {
            meshRenderer.enabled = false;
        }
    }

    private void Start()
    {
        if (expedition == null)
            expedition = ExpeditionController.Instance;

        if (expedition == null)
        {
            Debug.LogError("TunnelPassage needs an ExpeditionController in the scene.", this);
            enabled = false;
            return;
        }

        // Seed from the controller so the first real crossing isn't missed
        // or doubled.
        lastSide = expedition.IsOnExpedition ? Side.Maze : Side.Base;
    }

    private void Update()
    {
        Transform head = ResolveHead();

        if (head == null)
            return;

        Side side = GetSide(head.position);

        if (side == Side.Unknown || side == lastSide)
            return;

        Side previous = lastSide;
        lastSide = side;

        if (previous == Side.Base && side == Side.Maze)
            expedition.BeginExpedition();
        else if (previous == Side.Maze && side == Side.Base)
            expedition.EndExpedition();
    }

    private Transform ResolveHead()
    {
        if (explorerHead != null)
            return explorerHead;

        Camera main = Camera.main;
        return main != null ? main.transform : null;
    }

    private void GetLocalBox(out Vector3 center, out Vector3 halfSize)
    {
        if (box != null)
        {
            center = box.center;
            halfSize = box.size * 0.5f;
        }
        else
        {
            center = Vector3.zero;
            halfSize = passageSize * 0.5f;
        }
    }

    // Base / Maze while the head is in the zone beyond the band,
    // Unknown while it's inside the band or away from the passage.
    private Side GetSide(Vector3 worldPosition)
    {
        GetLocalBox(out Vector3 center, out Vector3 half);

        // Work in metres along the doorway axes, independent of scale.
        Vector3 offset = worldPosition - transform.TransformPoint(center);
        Vector3 right = transform.right;
        Vector3 forward = transform.forward;
        Vector3 scale = transform.lossyScale;

        float halfWidth = half.x * Mathf.Abs(scale.x) + widthMargin;
        float halfDepth = half.z * Mathf.Abs(scale.z);

        float lateral = Vector3.Dot(offset, right);
        float along = Vector3.Dot(offset, forward);

        if (Mathf.Abs(lateral) > halfWidth)
            return Side.Unknown;

        if (along > halfDepth && along < halfDepth + approachDistance)
            return Side.Maze;

        if (along < -halfDepth && along > -halfDepth - approachDistance)
            return Side.Base;

        return Side.Unknown;
    }

    private void OnDrawGizmos()
    {
        if (box == null)
            box = GetComponent<BoxCollider>();

        GetLocalBox(out Vector3 center, out Vector3 half);

        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
        Gizmos.DrawWireCube(center, half * 2f);

        // Arrow into the maze.
        Gizmos.matrix = Matrix4x4.identity;
        Vector3 from = transform.TransformPoint(center);
        Vector3 to = from + transform.forward * 1.5f;
        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(from, to);
        Gizmos.DrawLine(to, to - transform.forward * 0.3f + transform.right * 0.2f);
        Gizmos.DrawLine(to, to - transform.forward * 0.3f - transform.right * 0.2f);
    }
}
