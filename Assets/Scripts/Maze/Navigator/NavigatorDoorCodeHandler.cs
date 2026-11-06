
using UnityEngine;

public class NavigatorDoorCodeHandler : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private NavigatorCodeEntry codeEntry;
    [SerializeField] private Transform generatedMapRoot;

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
        // Only handle door codes.
        if (match.Action != ControlCodeAction.ToggleDoor)
            return;

        if (generatedMapRoot == null)
        {
            Debug.LogError("Generated Map Root is not assigned.", this);
            return;
        }

        // Find all doors in the generated Navigator map.
        NavigatorDoorNode[] doors =
            generatedMapRoot.GetComponentsInChildren<NavigatorDoorNode>(true);

        foreach (NavigatorDoorNode door in doors)
        {
            if (door.DoorId != match.DoorId)
                continue;

            // We found the correct door!
            door.ToggleState();
            return;
        }

        Debug.LogWarning(
            $"Could not find Navigator door {match.DoorId}",
            this
        );
    }
}
