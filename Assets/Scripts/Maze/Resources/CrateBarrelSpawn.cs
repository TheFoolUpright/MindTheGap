using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(MazeRefresh))]
public class CrateBarrelSpawn : MonoBehaviour
{
    private const int SpawnCount = 3;

    [SerializeField] private GameObject resourceSpawnerPrefab;

    private readonly Dictionary<GameObject, HashSet<Vector2Int>> previousCellsByLayout =
        new Dictionary<GameObject, HashSet<Vector2Int>>();
    private MazeRefresh mazeRefresh;

    private void Awake()
    {
        mazeRefresh = GetComponent<MazeRefresh>();
    }

    private void OnEnable()
    {
        mazeRefresh.LayoutChanged += SpawnForLayout;
    }

    private void OnDisable()
    {
        mazeRefresh.LayoutChanged -= SpawnForLayout;
    }

    private void SpawnForLayout(GameObject layoutPrefab, Transform layoutRoot)
    {
        if (resourceSpawnerPrefab == null)
        {
            Debug.LogWarning("Assign a resource spawner prefab.", this);
            return;
        }

        MazeLayoutData data = mazeRefresh.CurrentLayoutData;

        if (data == null || !data.IsConfigured)
        {
            Debug.LogWarning($"Assign valid layout data in MazeRefresh for {layoutPrefab.name}.", this);
            return;
        }

        var candidates = new List<Vector2Int>();
        var uniqueCells = new HashSet<Vector2Int>();
        foreach (Vector2Int cell in data.ResourceSpawnCells)
        {
            if (cell.x < 0 || cell.x >= data.Width || cell.y < 0 || cell.y >= data.Height ||
                !uniqueCells.Add(cell))
                continue;

            candidates.Add(cell);
        }

        int count = Mathf.Min(SpawnCount, candidates.Count);
        if (count < SpawnCount)
            Debug.LogWarning($"{layoutPrefab.name} has only {count} valid resource spawn cells.", this);

        previousCellsByLayout.TryGetValue(layoutPrefab, out HashSet<Vector2Int> previousCells);
        var selectedCells = new HashSet<Vector2Int>();

        // Shuffle only the slots we use and redraw if this layout gets the same set again.
        do
        {
            selectedCells.Clear();
            for (int i = 0; i < count; i++)
            {
                int randomIndex = Random.Range(i, candidates.Count);
                Vector2Int cell = candidates[randomIndex];
                candidates[randomIndex] = candidates[i];
                candidates[i] = cell;
                selectedCells.Add(cell);
            }
        }
        while (candidates.Count > count && previousCells != null && selectedCells.SetEquals(previousCells));

        previousCellsByLayout[layoutPrefab] = selectedCells;

        // MazeRefresh removes the old layout together with its spawned containers.
        Quaternion rotation = layoutRoot.rotation * resourceSpawnerPrefab.transform.localRotation;
        for (int i = 0; i < count; i++)
        {
            Vector2Int cell = candidates[i];
            Vector3 center = layoutRoot.TransformPoint(data.GetCellCenterLocal(cell.x, cell.y));
            GameObject instance = Instantiate(resourceSpawnerPrefab, center, rotation, layoutRoot);
            instance.name = $"{resourceSpawnerPrefab.name} ({cell.x}, {cell.y})";
        }
    }
}
