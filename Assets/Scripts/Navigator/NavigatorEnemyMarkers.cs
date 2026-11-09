using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[DisallowMultipleComponent]
[RequireComponent(typeof(NavigatorMapBuilder), typeof(NavigatorMazeController))]
public class NavigatorEnemyMarkers : MonoBehaviour
{
    [SerializeField] private Image markerTemplate;
    [SerializeField] private Color markerColor = new Color(1f, 0.24f, 0.2f);
    [SerializeField, Min(0f)] private float followSpeed = 15f;
    [SerializeField] private float rotationOffset = -45f;

    private readonly Dictionary<int, RectTransform> markers = new();
    private readonly HashSet<int> visibleIds = new();
    private readonly List<int> removedIds = new();
    private NavigatorMapBuilder mapBuilder;
    private NavigatorMazeController mazeController;
    private MazeNetworkState mazeState;
    private int displayedGeneration;

    private void Awake()
    {
        mapBuilder = GetComponent<NavigatorMapBuilder>();
        mazeController = GetComponent<NavigatorMazeController>();
    }

    private void LateUpdate()
    {
        MazeLayoutData layout = mazeController.CurrentLayout;
        if (markerTemplate == null || layout == null || mazeController.IsRefreshing)
        {
            ClearMarkers();
            return;
        }

        if (mazeState == null || !mazeState.IsSpawned)
            mazeState = FindAnyObjectByType<MazeNetworkState>();

        int generation = mazeController.AppliedGeneration;
        if (mazeState == null || !mazeState.IsSpawned || mazeState.StateGeneration != generation)
        {
            ClearMarkers();
            return;
        }

        if (displayedGeneration != generation)
        {
            ClearMarkers();
            displayedGeneration = generation;
        }

        visibleIds.Clear();
        float t = 1f - Mathf.Exp(-followSpeed * Time.unscaledDeltaTime);

        for (int i = 0; i < mazeState.EnemyPoseCount; i++)
        {
            NetworkEnemyPose pose = mazeState.GetEnemyPose(i);
            Vector2 cell = pose.CellPosition;
            if (pose.Generation != generation || cell.x < 0f || cell.y < 0f ||
                cell.x >= layout.Width || cell.y >= layout.Height)
            {
                continue;
            }

            visibleIds.Add(pose.Id);
            Vector2 target = cell * mapBuilder.UnitsPerCell;
            if (!markers.TryGetValue(pose.Id, out RectTransform marker))
            {
                Image image = Instantiate(markerTemplate, markerTemplate.transform.parent, false);
                image.name = "EnemyMarker";
                image.color = markerColor;
                image.raycastTarget = false;
                marker = image.rectTransform;
                marker.anchoredPosition = target;
                image.gameObject.SetActive(true);
                markers.Add(pose.Id, marker);
            }

            marker.anchoredPosition = Vector2.Lerp(marker.anchoredPosition, target, t);
            marker.localRotation = Quaternion.Euler(0f, 0f, -pose.FacingDegrees + rotationOffset);
        }

        removedIds.Clear();
        foreach (var entry in markers)
        {
            if (!visibleIds.Contains(entry.Key))
                removedIds.Add(entry.Key);
        }

        foreach (int id in removedIds)
        {
            markers[id].gameObject.SetActive(false);
            Destroy(markers[id].gameObject);
            markers.Remove(id);
        }
    }

    private void OnDisable()
    {
        ClearMarkers();
    }

    private void ClearMarkers()
    {
        foreach (RectTransform marker in markers.Values)
        {
            if (marker != null)
            {
                marker.gameObject.SetActive(false);
                Destroy(marker.gameObject);
            }
        }

        markers.Clear();
    }
}
