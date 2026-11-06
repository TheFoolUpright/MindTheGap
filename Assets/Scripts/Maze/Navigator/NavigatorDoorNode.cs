using UnityEngine;
using UnityEngine.UI;

public class NavigatorDoorNode : MonoBehaviour
{
    [Header("Door Identity")]
    [SerializeField] private int edgeX;
    [SerializeField] private int edgeY;
    [SerializeField] private bool isVertical;

    [Header("Door Appearance")]
    [SerializeField] private Image segmentImage;
    [SerializeField] private Image nodeImage;

    [SerializeField]
    private Color closedColor =
        new Color(0.8f, 0.5f, 0.2f);

    [SerializeField]
    private Color openColor =
        new Color(0.2f, 0.8f, 0.5f);

    [SerializeField]
    private MazeDoorState currentState =
        MazeDoorState.Closed;

    public MazeDoorState CurrentState => currentState;
    public int EdgeX => edgeX;
    public int EdgeY => edgeY;
    public bool IsVertical => isVertical;

    public string DoorId =>
        $"{(isVertical ? "V" : "H")}_{edgeX}_{edgeY}";

    public void Configure(int x, int y, bool vertical)
    {
        edgeX = x;
        edgeY = y;
        isVertical = vertical;
    }

    private void OnEnable()
    {
        UpdateAppearance();

    }


    public void SetState(MazeDoorState newState)
    {
        currentState = newState;
        UpdateAppearance();

    }




    public void ToggleState()
    {
        SetState(currentState == MazeDoorState.Closed
            ? MazeDoorState.Open
            : MazeDoorState.Closed);
    }

    public void ResetForRefresh()
    {
        SetState(MazeDoorState.Closed);
    }

    private void UpdateAppearance()
    {
        bool isClosed = currentState == MazeDoorState.Closed;

        // Hide the line when the door opens.
        if (segmentImage != null)
            segmentImage.enabled = isClosed;

        // Keep the circular node visible, but change its color.
        if (nodeImage != null)
            nodeImage.color = isClosed ? closedColor : openColor;
    }

    // Temporary testing commands.
    [ContextMenu("Preview Door/Open")]
    private void PreviewOpen()
    {
        SetState(MazeDoorState.Open);
    }

    [ContextMenu("Preview Door/Closed")]
    private void PreviewClosed()
    {
        SetState(MazeDoorState.Closed);
    }
}
