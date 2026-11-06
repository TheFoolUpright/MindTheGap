
using UnityEngine;
using UnityEngine.UI;

public class NavigatorLightNode : MonoBehaviour
{
    [Header("Light Identity")]
    [SerializeField] private int lightId;

    [Header("UI References")]
    [SerializeField] private Image nodeImage;
    [SerializeField] private Image iconImage;
    [SerializeField] private GameObject brokenOverlay;

    [Header("On Appearance")]
    [SerializeField]
    private Color onColor =
        new Color(1f, 0.8f, 0.2f);

    [Header("Off Appearance")]
    [SerializeField]
    private Color offColor =
        new Color(0.4f, 0.45f, 0.5f);

    [Header("Broken Appearance")]
    [SerializeField]
    private Color brokenColor =
        new Color(0.65f, 0.15f, 0.15f);

    [Header("Current State")]
    [SerializeField]
    private MazeLightState currentState =
        MazeLightState.On;

    public int LightId => lightId;
    public MazeLightState CurrentState => currentState;

    private void OnEnable()
    {
        UpdateAppearance();
    }

    // Called by NavigatorMapBuilder when creating the light.
    public void Configure(int id)
    {
        lightId = id;
        ResetForRefresh();
    }

    public void SetState(MazeLightState newState)
    {
        // A broken light cannot be repaired during the same maze cycle.
        if (currentState == MazeLightState.Broken &&
            newState != MazeLightState.Broken)
        {
            return;
        }

        currentState = newState;
        UpdateAppearance();
    }

    // Called when the maze refreshes.
    public void ResetForRefresh()
    {
        currentState = MazeLightState.On;
        UpdateAppearance();
    }

    private void UpdateAppearance()
    {
        Color color = currentState switch
        {
            MazeLightState.On => onColor,
            MazeLightState.Off => offColor,
            MazeLightState.Broken => brokenColor,
            _ => offColor
        };

        if (nodeImage != null)
            nodeImage.color = color;

        if (iconImage != null)
            iconImage.color = color;

        if (brokenOverlay != null)
            brokenOverlay.SetActive(
                currentState == MazeLightState.Broken
            );
    }

    // Temporary Inspector testing tools.
    [ContextMenu("Preview Light/On")]
    private void PreviewOn()
    {
        ResetForRefresh();
    }

    [ContextMenu("Preview Light/Off")]
    private void PreviewOff()
    {
        SetState(MazeLightState.Off);
    }

    [ContextMenu("Preview Light/Broken")]
    private void PreviewBroken()
    {
        SetState(MazeLightState.Broken);
    }
}
