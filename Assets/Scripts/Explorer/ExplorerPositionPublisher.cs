
using System.Collections;
using UnityEngine;

public class ExplorerPositionPublisher : MonoBehaviour
{
    [Header("Explorer References")]
    [SerializeField] private Transform explorerHead;
    [SerializeField] private Transform generatedMazeRoot;

    [Header("Network Settings")]
    [SerializeField, Min(0.02f)]
    private float publishInterval = 0.1f;

    [SerializeField, Min(0f)]
    private float minimumMovement = 0.05f;

    [SerializeField, Min(0f)]
    private float minimumTurnDegrees = 2f;

    private float lastPublishedFacing;
    private bool hasPublishedFacing;

    private MazeNetworkState mazeNetworkState;

    private float nextPublishTime;
    private Vector2 lastPublishedPosition;
    private bool hasPublished;

    private ExplorerMazeBuilder mazeBuilder;
    private bool warnedAboutCellSize;

    // Set by MazeRefresh. When > 0 it replaces the ExplorerMazeBuilder lookup.
    private float runtimeCellSize;

    // Called by MazeRefresh after each new maze is instantiated.
    public void SetRoot(Transform newRoot, float cellSize)
    {
        generatedMazeRoot = newRoot;
        runtimeCellSize = cellSize;
        mazeBuilder = null;

        // Publish the new position straight away.
        hasPublished = false;
        hasPublishedFacing = false;
        nextPublishTime = 0f;
    }

    private void OnEnable()
    {
        if (generatedMazeRoot != null)
        {
            mazeBuilder =
                generatedMazeRoot.GetComponentInParent<ExplorerMazeBuilder>();
        }

        StartCoroutine(FindNetworkState());
    }

    // Metres per maze cell in the geometry the Explorer is walking through.
    private float GetBuiltCellSize()
    {
        if (runtimeCellSize > 0f)
            return runtimeCellSize;

        if (mazeBuilder == null)
            return 0f;

        if (!warnedAboutCellSize && !mazeBuilder.BuiltCellSizeMatchesLayout)
        {
            warnedAboutCellSize = true;
            Debug.LogWarning(
                $"Explorer maze was built with {mazeBuilder.BuiltCellSize} m cells, " +
                $"but '{mazeBuilder.Layout.name}' now says {mazeBuilder.Layout.CellSize} m. " +
                "Regenerate the ExplorerMaze prefab so walls, lights and enemy spawns " +
                "match the layout. The Navigator marker uses the built size meanwhile.",
                mazeBuilder);
        }

        return mazeBuilder.BuiltCellSize;
    }

    private IEnumerator FindNetworkState()
    {
        while (mazeNetworkState == null ||
               !mazeNetworkState.IsSpawned)
        {
            MazeNetworkState found =
                FindAnyObjectByType<MazeNetworkState>();

            if (found != null && found.IsSpawned)
            {
                mazeNetworkState = found;
                break;
            }

            yield return new WaitForSeconds(0.2f);
        }
    }


    private void Update()
    {
        if (explorerHead == null || generatedMazeRoot == null)
            return;

        if (mazeNetworkState == null ||
            !mazeNetworkState.IsSpawned ||
            !mazeNetworkState.IsServer)
            return;

        if (Time.unscaledTime < nextPublishTime)
            return;

        nextPublishTime = Time.unscaledTime + publishInterval;

        // POSITION
        // Headset position relative to the maze root, in metres.
        // (0,0) is the outer corner of cell (0,0); X = layout X, Z = layout Y.
        Vector3 localPosition =
            generatedMazeRoot.InverseTransformPoint(
                explorerHead.position
            );

        float cellSize = GetBuiltCellSize();

        if (cellSize > 0f)
        {
            // Publish in maze cell units, so the Navigator doesn't need to
            // know how big the Explorer's geometry is. Cell (x, y) spans
            // [x, x+1) x [y, y+1); its centre is (x + 0.5, y + 0.5).
            Vector2 cellPosition = new Vector2(
                localPosition.x,
                localPosition.z
            ) / cellSize;

            if (!hasPublished ||
                Vector2.Distance(cellPosition, lastPublishedPosition) * cellSize
                    >= minimumMovement)
            {
                mazeNetworkState.PublishExplorerPosition(cellPosition);

                lastPublishedPosition = cellPosition;
                hasPublished = true;
            }
        }

        // FACING DIRECTION
        Vector3 localForward =
            generatedMazeRoot.InverseTransformDirection(
                explorerHead.forward
            );

        // Ignore looking up or down.
        localForward.y = 0f;

        if (localForward.sqrMagnitude < 0.0001f)
            return;

        float facingDegrees = Mathf.Atan2(
            localForward.x,
            localForward.z
        ) * Mathf.Rad2Deg;

        if (!hasPublishedFacing ||
            Mathf.Abs(Mathf.DeltaAngle(
                lastPublishedFacing,
                facingDegrees
            )) >= minimumTurnDegrees)
        {
            mazeNetworkState.PublishExplorerFacing(facingDegrees);

            lastPublishedFacing = facingDegrees;
            hasPublishedFacing = true;
        }
    }
}
