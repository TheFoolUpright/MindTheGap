
using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public struct FurnaceState
{
    public int totalCubes;
    public int totalPyramids;
    public int pendingCubes;
    public int pendingPyramids;
}

public class FurnaceController : MonoBehaviour
{
    [Header("Prototype Settings")]
    [SerializeField]
    private bool processOnEntry = false;

    private readonly HashSet<FurnaceResource> contents = new();

    private int totalCubes;
    private int totalPyramids;

    public event Action<FurnaceState> StateChanged;

    public FurnaceState State
    {
        get
        {
            int pendingCubes = 0;
            int pendingPyramids = 0;

            foreach (FurnaceResource resource in contents)
            {
                if (resource == null)
                    continue;

                if (resource.ResourceType == FurnaceResourceType.Cube)
                    pendingCubes++;
                else
                    pendingPyramids++;
            }

            return new FurnaceState
            {
                totalCubes = totalCubes,
                totalPyramids = totalPyramids,
                pendingCubes = pendingCubes,
                pendingPyramids = pendingPyramids
            };
        }
    }

    public void AddResource(FurnaceResource resource)
    {
        if (resource == null || !contents.Add(resource))
            return;

        Debug.Log($"Furnace detected: {resource.ResourceType}");

        StateChanged?.Invoke(State);

        if (processOnEntry)
            ProcessContents();
    }

    public void RemoveResource(FurnaceResource resource)
    {
        if (contents.Remove(resource))
            StateChanged?.Invoke(State);
    }

    [ContextMenu("Process Furnace Contents")]
    public void ProcessContents()
    {
        if (contents.Count == 0)
            return;

        List<FurnaceResource> toConsume = new(contents);

        int addedCubes = 0;
        int addedPyramids = 0;

        foreach (FurnaceResource resource in toConsume)
        {
            if (resource == null)
                continue;

            if (resource.ResourceType == FurnaceResourceType.Cube)
                addedCubes++;
            else
                addedPyramids++;
        }

        contents.Clear();

        totalCubes += addedCubes;
        totalPyramids += addedPyramids;

        foreach (FurnaceResource resource in toConsume)
        {
            if (resource != null)
                Destroy(resource.gameObject);
        }

        Debug.Log(
            $"Furnace processed: {addedCubes} cubes, " +
            $"{addedPyramids} pyramids. " +
            $"Totals: {totalCubes} cubes, {totalPyramids} pyramids."
        );

        StateChanged?.Invoke(State);
    }
}
