
using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Navigator side of the maze refresh. Follows MazeRefreshNetworkState:
// Preparing -> show the "maze shifting" overlay.
// Ready     -> swap the map prefab under MapContent, rebind doors/lights,
//              regenerate codes, focus on the entrance, hide the overlay.
public class NavigatorMazeController : MonoBehaviour
{
    private const string MapInstanceName = "Generated Map";

    [Header("Layouts")]
    [SerializeField] private MazePrefabLibrary library;

    [Header("Map")]
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private NavigatorMapBuilder mapBuilder;
    [SerializeField] private NavigatorMapCursorPan cursorPan;

    [Header("Map Systems")]
    [SerializeField] private NavigatorControlCodeManager codeManager;
    [SerializeField] private MazeDoorNetworkBinding doorBinding;
    [SerializeField] private MazeLightNetworkBinding lightBinding;

    [Header("Maze Shifting Overlay")]
    [Tooltip("Optional. If empty, a simple overlay is created over the map viewport.")]
    [SerializeField] private GameObject shiftingOverlay;
    [Tooltip("Parent for the generated overlay. Defaults to MapContent's parent.")]
    [SerializeField] private RectTransform overlayParent;
    [SerializeField] private string overlayMessage = "MAZE SHIFTING...";
    [SerializeField, Min(0f)] private float minimumOverlaySeconds = 0.75f;

    public MazeLayoutData CurrentLayout { get; private set; }
    public int AppliedGeneration { get; private set; }
    public bool IsRefreshing { get; private set; } = true;

    public event Action<MazeLayoutData> MapSwapped;

    private MazeRefreshNetworkState refreshState;
    private MazeNetworkState mazeState;
    private Coroutine connectRoutine;
    private Coroutine applyRoutine;
    private float overlayShownAt;

    private void OnEnable()
    {
        // Covers the placeholder map until the host's layout arrives.
        ShowOverlay();
        connectRoutine = StartCoroutine(ConnectWhenReady());
    }

    private void OnDisable()
    {
        if (connectRoutine != null)
            StopCoroutine(connectRoutine);

        if (applyRoutine != null)
            StopCoroutine(applyRoutine);

        connectRoutine = null;
        applyRoutine = null;

        if (refreshState != null)
            refreshState.LayoutChanged -= OnSnapshotChanged;

        refreshState = null;
    }

    private IEnumerator ConnectWhenReady()
    {
        // GameSession and the role scenes load separately.
        while (MazeRefreshNetworkState.Instance == null ||
               !MazeRefreshNetworkState.Instance.IsSpawned)
        {
            yield return new WaitForSecondsRealtime(0.2f);
        }

        refreshState = MazeRefreshNetworkState.Instance;
        refreshState.LayoutChanged += OnSnapshotChanged;
        connectRoutine = null;

        // Late join: apply whatever the host has already published.
        OnSnapshotChanged(refreshState.Current.Value);
    }

    private void OnSnapshotChanged(MazeRefreshSnapshot snapshot)
    {
        if (snapshot.Generation <= 0)
            return;

        switch (snapshot.Phase)
        {
            case MazeRefreshPhase.Preparing:
                IsRefreshing = true;
                ShowOverlay();
                break;

            case MazeRefreshPhase.Ready:
                if (snapshot.Generation == AppliedGeneration)
                    return;

                if (applyRoutine != null)
                    StopCoroutine(applyRoutine);

                applyRoutine = StartCoroutine(ApplyWhenStateReady(snapshot));
                break;
        }
    }

    private IEnumerator ApplyWhenStateReady(MazeRefreshSnapshot snapshot)
    {
        IsRefreshing = true;
        ShowOverlay();

        // Give other Navigator scripts one frame to run Start.
        yield return null;

        while (mazeState == null || !mazeState.IsSpawned)
        {
            mazeState = FindAnyObjectByType<MazeNetworkState>();

            if (mazeState != null && mazeState.IsSpawned)
                break;

            yield return new WaitForSecondsRealtime(0.2f);
        }

        // Wait until the door/light lists for this generation have arrived.
        while (mazeState.StateGeneration < snapshot.Generation)
            yield return null;

        // A newer refresh started while we were waiting.
        if (refreshState == null ||
            refreshState.Current.Value.Generation != snapshot.Generation)
        {
            applyRoutine = null;
            yield break;
        }

        if (library == null ||
            !library.TryGetLayout(snapshot.LayoutId, out MazePrefabEntry entry) ||
            entry.navigatorPrefab == null)
        {
            Debug.LogError(
                $"No Navigator map for layout {snapshot.LayoutId} in the library.",
                this
            );
            applyRoutine = null;
            yield break;
        }

        SwapMap(entry);
        AppliedGeneration = snapshot.Generation;

        Debug.Log(
            $"[Navigator] Map swapped to '{entry.navigatorPrefab.name}' ({snapshot}).",
            this
        );

        // Keep the overlay up long enough to read.
        float remaining =
            minimumOverlaySeconds - (Time.unscaledTime - overlayShownAt);

        if (remaining > 0f)
            yield return new WaitForSecondsRealtime(remaining);

        // Only hide if no newer refresh started meanwhile.
        if (refreshState != null &&
            refreshState.Current.Value.Generation == AppliedGeneration &&
            refreshState.Current.Value.Phase == MazeRefreshPhase.Ready)
        {
            HideOverlay();
            IsRefreshing = false;
        }

        applyRoutine = null;
    }

    private void SwapMap(MazePrefabEntry entry)
    {
        MazeLayoutData layout = entry.layoutData;

        // Remove the previous map. Detach first so the bindings
        // can't find its doors and lights before Destroy runs.
        Transform previous = mapContent.Find(MapInstanceName);
        int siblingIndex = previous != null ? previous.GetSiblingIndex() : 0;

        if (previous != null)
        {
            previous.gameObject.SetActive(false);
            previous.SetParent(null, false);
            Destroy(previous.gameObject);
        }

        GameObject map = Instantiate(entry.navigatorPrefab, mapContent, false);
        map.name = MapInstanceName;
        map.transform.SetSiblingIndex(siblingIndex);

        // Map size follows the layout, which also updates the pan limits.
        float units = mapBuilder != null ? mapBuilder.UnitsPerCell : 1f;

        mapContent.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal, layout.Width * units);
        mapContent.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, layout.Height * units);

        // The marker, pan and hover read the layout through the builder.
        if (mapBuilder != null)
            mapBuilder.SetRuntimeLayout(layout);

        if (doorBinding != null)
            doorBinding.SetRoot(map.transform);

        if (lightBinding != null)
            lightBinding.SetRoot(map.transform);

        if (codeManager != null)
            codeManager.SetLayoutAndRegenerate(layout);

        Canvas.ForceUpdateCanvases();

        if (cursorPan != null)
            cursorPan.FocusOnEntrance();

        CurrentLayout = layout;
        MapSwapped?.Invoke(layout);
    }

    private void ShowOverlay()
    {
        if (shiftingOverlay == null)
            shiftingOverlay = CreateDefaultOverlay();

        if (shiftingOverlay == null)
            return;

        if (!shiftingOverlay.activeSelf)
            overlayShownAt = Time.unscaledTime;

        shiftingOverlay.transform.SetAsLastSibling();
        shiftingOverlay.SetActive(true);
    }

    private void HideOverlay()
    {
        if (shiftingOverlay != null)
            shiftingOverlay.SetActive(false);
    }

    // Prototype overlay: a dark panel with a centred label.
    private GameObject CreateDefaultOverlay()
    {
        RectTransform parent = overlayParent != null
            ? overlayParent
            : mapContent != null ? mapContent.parent as RectTransform : null;

        if (parent == null)
            return null;

        GameObject overlay = new GameObject(
            "Maze Shifting Overlay",
            typeof(RectTransform),
            typeof(Image)
        );

        RectTransform overlayRect = (RectTransform)overlay.transform;
        overlayRect.SetParent(parent, false);
        Stretch(overlayRect);

        Image background = overlay.GetComponent<Image>();
        background.color = new Color(0f, 0f, 0f, 0.8f);
        background.raycastTarget = false;

        GameObject label = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI)
        );

        RectTransform labelRect = (RectTransform)label.transform;
        labelRect.SetParent(overlayRect, false);
        Stretch(labelRect);

        TextMeshProUGUI text = label.GetComponent<TextMeshProUGUI>();
        text.text = overlayMessage;
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 48f;
        text.color = Color.white;
        text.raycastTarget = false;

        return overlay;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        rect.pivot = new Vector2(0.5f, 0.5f);
    }
}
