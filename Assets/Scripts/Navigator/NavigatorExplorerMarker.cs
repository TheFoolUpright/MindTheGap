
using System.Collections;
using UnityEngine;

public class NavigatorExplorerMarker : MonoBehaviour
{
    [Header("Map References")]
    [SerializeField] private NavigatorMapBuilder mapBuilder;
    [SerializeField] private RectTransform explorerMarker;

    [Header("Movement")]
    [SerializeField, Min(0f)]
    private float followSpeed = 15f;

    private MazeNetworkState mazeNetworkState;
    private Coroutine connectionCoroutine;

    private Vector2 targetPosition;
    private bool hasTarget;

    private void OnEnable()
    {
        if (explorerMarker != null)
            explorerMarker.gameObject.SetActive(false);

        connectionCoroutine = StartCoroutine(ConnectWhenReady());
    }

    private IEnumerator ConnectWhenReady()
    {
        while (mazeNetworkState == null)
        {
            MazeNetworkState found =
                FindAnyObjectByType<MazeNetworkState>();

            if (found != null && found.IsSpawned)
            {
                mazeNetworkState = found;
                break;
            }

            yield return new WaitForSecondsRealtime(0.2f);
        }

        // Subscribe to future position changes.
        mazeNetworkState.ExplorerPositionChanged +=
            OnExplorerPositionChanged;

        // Also apply the current position immediately.
        OnExplorerPositionChanged(
            mazeNetworkState.ExplorerMazePosition
        );

        connectionCoroutine = null;
    }

    private void OnExplorerPositionChanged(Vector2 mazePosition)
    {
        if (mazeNetworkState == null ||
            !mazeNetworkState.HasExplorerPosition ||
            mapBuilder == null ||
            mapBuilder.Layout == null ||
            explorerMarker == null)
        {
            HideMarker();
            return;
        }

        MazeLayoutData layout = mapBuilder.Layout;

        float mazeWidth = layout.Width * layout.CellSize;
        float mazeHeight = layout.Height * layout.CellSize;

        // Only display the Explorer inside the map boundaries.
        bool insideMaze =
            mazePosition.x >= 0f &&
            mazePosition.y >= 0f &&
            mazePosition.x < mazeWidth &&
            mazePosition.y < mazeHeight;

        if (!insideMaze)
        {
            HideMarker();
            return;
        }

        // Convert maze metres into Navigator UI units.
        float scale = mapBuilder.UnitsPerCell / layout.CellSize;

        targetPosition = mazePosition * scale;

        // Snap the marker to its first valid position.
        if (!hasTarget)
        {
            explorerMarker.anchoredPosition = targetPosition;
            hasTarget = true;
        }

        explorerMarker.gameObject.SetActive(true);
    }

    private void Update()
    {
        if (!hasTarget || explorerMarker == null)
            return;

        // Smooth movement between network updates.
        float t = 1f - Mathf.Exp(
            -followSpeed * Time.unscaledDeltaTime
        );

        explorerMarker.anchoredPosition = Vector2.Lerp(
            explorerMarker.anchoredPosition,
            targetPosition,
            t
        );
    }

    private void HideMarker()
    {
        hasTarget = false;

        if (explorerMarker != null)
            explorerMarker.gameObject.SetActive(false);
    }

    private void OnDisable()
    {
        if (connectionCoroutine != null)
        {
            StopCoroutine(connectionCoroutine);
            connectionCoroutine = null;
        }

        if (mazeNetworkState != null)
        {
            mazeNetworkState.ExplorerPositionChanged -=
                OnExplorerPositionChanged;

            mazeNetworkState = null;
        }

        HideMarker();
    }
}
