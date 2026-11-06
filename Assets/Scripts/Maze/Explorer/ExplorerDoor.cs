
using UnityEngine;
using UnityEngine.AI;

public class ExplorerDoor : MonoBehaviour
{
    [Header("Door Identity")]
    [SerializeField] private int edgeX;
    [SerializeField] private int edgeY;
    [SerializeField] private bool isVertical;

    [Header("Door Appearance")]
    [SerializeField] private Renderer doorRenderer;
    [SerializeField] private Material closedMaterial;
    [SerializeField] private Material openMaterial;

    [Header("Blocking Components")]
    [SerializeField] private Collider[] blockingColliders;
    [SerializeField] private NavMeshObstacle navMeshObstacle;

    [Header("Door State")]
    [SerializeField]
    private MazeDoorState currentState =
        MazeDoorState.Closed;

    public MazeDoorState CurrentState => currentState;

    public string DoorId =>
        $"{(isVertical ? "V" : "H")}_{edgeX}_{edgeY}";

    // Called by the maze builder when creating doors.
    public void Configure(int x, int y, bool vertical)
    {
        edgeX = x;
        edgeY = y;
        isVertical = vertical;
    }

    private void OnEnable()
    {
        UpdateDoorAppearance();
    }

    public void SetState(MazeDoorState newState)
    {
        currentState = newState;
        UpdateDoorAppearance();
    }

    public void ResetForRefresh()
    {
        SetState(MazeDoorState.Closed);
    }

    private void UpdateDoorAppearance()
    {
        bool isClosed = currentState == MazeDoorState.Closed;

        // Change the material.
        if (doorRenderer != null)
        {
            Material targetMaterial =
                isClosed ? closedMaterial : openMaterial;

            if (targetMaterial != null)
                doorRenderer.sharedMaterial = targetMaterial;
        }

        // Open doors should not physically block movement.
        if (blockingColliders != null)
        {
            foreach (Collider col in blockingColliders)
            {
                if (col != null)
                    col.enabled = isClosed;
            }
        }

        // Open doors should not block NavMesh paths.
        if (navMeshObstacle != null)
            navMeshObstacle.enabled = isClosed;
    }

    // Temporary testing commands.
    [ContextMenu("Preview Door/Open")]
    private void PreviewOpen()
    {
        if (Application.isPlaying)
            SetState(MazeDoorState.Open);
    }

    [ContextMenu("Preview Door/Closed")]
    private void PreviewClosed()
    {
        if (Application.isPlaying)
            SetState(MazeDoorState.Closed);
    }
}
