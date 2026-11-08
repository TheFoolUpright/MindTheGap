
using UnityEngine;

public class NavigatorLightCodeHandler : MonoBehaviour
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
        // Ignore valid door codes.
        if (match.Action != ControlCodeAction.ToggleLight &&
            match.Action != ControlCodeAction.OverloadLight)
        {
            return;
        }

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

        switch (match.Action)
        {
            case ControlCodeAction.ToggleLight:
                mazeNetworkState.RequestToggleLight(match.LightId);
                break;

            case ControlCodeAction.OverloadLight:
                mazeNetworkState.RequestOverloadLight(match.LightId);
                break;
        }
    }
}
