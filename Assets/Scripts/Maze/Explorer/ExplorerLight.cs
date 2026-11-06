
using UnityEngine;

public class ExplorerLight : MonoBehaviour
{
    [Header("Light Identity")]
    [SerializeField] private int lightId;

    [Header("Light Components")]
    [SerializeField] private Light lightSource;

    // Optional visual indicator for a broken light.
    [SerializeField] private GameObject brokenIndicator;

    [Header("Light State")]
    [SerializeField]
    private MazeLightState currentState =
        MazeLightState.On;

    public int LightId => lightId;
    public MazeLightState CurrentState => currentState;

    // Called by ExplorerMazeBuilder.
    public void Configure(int id)
    {
        lightId = id;
        ResetForRefresh();
    }

    private void OnEnable()
    {
        UpdateAppearance();
    }

    public void SetState(MazeLightState newState)
    {
        // Broken lights cannot be switched back on or off.
        if (currentState == MazeLightState.Broken &&
            newState != MazeLightState.Broken)
        {
            return;
        }

        currentState = newState;
        UpdateAppearance();
    }

    // Used when a new maze cycle begins.
    public void ResetForRefresh()
    {
        currentState = MazeLightState.On;
        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        // Only an On light should emit light.
        if (lightSource != null)
        {
            lightSource.enabled =
                currentState == MazeLightState.On;
        }

        // Optional broken-light visual.
        if (brokenIndicator != null)
        {
            brokenIndicator.SetActive(
                currentState == MazeLightState.Broken
            );
        }
    }

    // Temporary testing commands.
    [ContextMenu("Preview Light/On")]
    private void PreviewOn()
    {
        if (Application.isPlaying)
            SetState(MazeLightState.On);
    }

    [ContextMenu("Preview Light/Off")]
    private void PreviewOff()
    {
        if (Application.isPlaying)
            SetState(MazeLightState.Off);
    }

    [ContextMenu("Preview Light/Broken")]
    private void PreviewBroken()
    {
        if (Application.isPlaying)
            SetState(MazeLightState.Broken);
    }

    [ContextMenu("Preview Light/Reset for Refresh")]
    private void PreviewReset()
    {
        if (Application.isPlaying)
            ResetForRefresh();
    }
}
