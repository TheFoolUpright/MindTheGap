
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class MazePrefabEntry
{
    [Header("Shared Layout")]
    public MazeLayoutData layoutData;

    [Header("Role Prefabs")]
    public GameObject explorerPrefab;
    public GameObject navigatorPrefab;

    // The ID comes from the shared layout data.
    public int LayoutId =>
        layoutData != null ? layoutData.LayoutId : -1;
}

[CreateAssetMenu(
    fileName = "MazePrefabLibrary",
    menuName = "Mind the Gap/Maze Prefab Library"
)]
public class MazePrefabLibrary : ScriptableObject
{
    [SerializeField]
    private List<MazePrefabEntry> layouts = new();

    public IReadOnlyList<MazePrefabEntry> Layouts => layouts;

    // Find the maze assets using its shared layout ID.
    public bool TryGetLayout(
        int layoutId,
        out MazePrefabEntry result)
    {
        foreach (MazePrefabEntry entry in layouts)
        {
            if (entry == null || entry.layoutData == null)
                continue;

            if (entry.LayoutId == layoutId)
            {
                result = entry;
                return true;
            }
        }

        result = null;
        return false;
    }

    // Check all the entries before using the library.
    [ContextMenu("Validate Maze Library")]
    public void ValidateLibrary()
    {
        HashSet<int> usedIds = new();

        foreach (MazePrefabEntry entry in layouts)
        {
            if (entry == null ||
                entry.layoutData == null ||
                entry.explorerPrefab == null ||
                entry.navigatorPrefab == null)
            {
                Debug.LogError(
                    "Maze library contains an incomplete entry.",
                    this
                );
                continue;
            }

            if (!entry.layoutData.IsConfigured)
            {
                Debug.LogError(
                    $"Maze layout {entry.LayoutId} is not configured.",
                    this
                );
            }

            if (!usedIds.Add(entry.LayoutId))
            {
                Debug.LogError(
                    $"Duplicate maze layout ID: {entry.LayoutId}",
                    this
                );
            }
        }

        Debug.Log(
            $"Maze library validation completed. " +
            $"{layouts.Count} entries checked.",
            this
        );
    }
}
