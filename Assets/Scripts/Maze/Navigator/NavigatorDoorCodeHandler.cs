
using UnityEngine;

public class NavigatorDoorCodeHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavigatorCodeEntry codeEntry;

    private void OnEnable()
    {
        if (codeEntry != null)
            codeEntry.ValidCodeEntered += OnValidCodeEntered;
    }

    private void OnDisable()
    {
        if (codeEntry != null)
            codeEntry.ValidCodeEntered -= OnValidCodeEntered;
    }

    private void OnValidCodeEntered(ControlCodeMatch match)
    {
        // Only handle door control codes.
        if (match.Action != ControlCodeAction.ToggleDoor)
            return;

        MazeNetworkState mazeNetworkState =
            FindAnyObjectByType<MazeNetworkState>();

        if (mazeNetworkState == null || !mazeNetworkState.IsSpawned)
        {
            Debug.LogWarning(
                "MazeNetworkState is not ready.",
                this
            );
            return;
        }

        // Ask the host to toggle the door.
        // Do not change the Navigator door locally.
        mazeNetworkState.RequestToggleDoor(match.DoorId);
    }
}
