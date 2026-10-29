
using TMPro;
using UnityEngine;

public class FurnaceDisplay : MonoBehaviour
{
    [Header("Local Source (Explorer Only)")]
    [SerializeField] private FurnaceController furnace;

    [Header("Cube Bar")]
    [SerializeField] private RectTransform cubeBarBack;
    [SerializeField] private RectTransform cubeBarFill;

    [Header("Pyramid Bar")]
    [SerializeField] private RectTransform pyramidBarBack;
    [SerializeField] private RectTransform pyramidBarFill;

    [Header("Optional Text Displays")]
    [SerializeField] private TMP_Text totalsText;
    [SerializeField] private TMP_Text pendingText;

    private void OnEnable()
    {
        // Explorer: subscribe to the local furnace.
        // Navigator: state will arrive through TCP later.
        if (furnace != null)
        {
            furnace.StateChanged += ShowState;
            ShowState(furnace.State);
        }
    }

    private void OnDisable()
    {
        if (furnace != null)
            furnace.StateChanged -= ShowState;
    }

    // Updates the display from a FurnaceState.
    // Can be called locally or via TCP.
    public void ShowState(FurnaceState state)
    {
        // Cube progress
        SetBar(
            cubeBarBack,
            cubeBarFill,
            state.totalCubes,
            state.cubeGoal
        );

        // Pyramid progress
        SetBar(
            pyramidBarBack,
            pyramidBarFill,
            state.totalPyramids,
            state.pyramidGoal
        );

        // Optional processed totals display
        if (totalsText != null)
        {
            totalsText.text =
                $"Cubes: {state.totalCubes}/{state.cubeGoal}\n" +
                $"Pyramids: {state.totalPyramids}/{state.pyramidGoal}";
        }

        // Optional pending resources display
        if (pendingText != null)
        {
            pendingText.text =
                $"Pending: {state.pendingCubes} cubes, " +
                $"{state.pendingPyramids} pyramids";
        }
    }

    private static void SetBar(
        RectTransform back,
        RectTransform fill,
        int current,
        int goal)
    {
        if (back == null || fill == null)
            return;

        float fraction = goal > 0
            ? Mathf.Clamp01((float)current / goal)
            : 0f;

        float fullWidth = back.rect.width;
        float fillWidth = fullWidth * fraction;

        float leftEdge =
            back.anchoredPosition.x -
            back.pivot.x * fullWidth;

        fill.sizeDelta = new Vector2(
            fillWidth,
            fill.sizeDelta.y
        );

        fill.anchoredPosition = new Vector2(
            leftEdge + fill.pivot.x * fillWidth,
            fill.anchoredPosition.y
        );
    }
}
