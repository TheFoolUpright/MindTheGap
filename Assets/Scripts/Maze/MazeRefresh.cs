using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEngine;
using UnityEngine.InputSystem;

[DisallowMultipleComponent]
public class MazeRefresh : MonoBehaviour
{
    // For enemies to know when the layout has changed so they can update their pathfinding.
    public event System.Action<GameObject, Transform> LayoutChanged;
    [SerializeField] private GameObject[] layoutPrefabs = new GameObject[0];
    [SerializeField] private float refreshInterval = 60f;
    [SerializeField] private float secondsRemaining;

    // using CYCLE allows for a more efficient shuffle and avoids the need to create a new list every time.
    private readonly List<GameObject> cycle = new List<GameObject>();
    private GameObject activeLayout;
    private int currentIndex;
    private float nextRefreshTime;

    private void Start()
    {
        foreach (GameObject prefab in layoutPrefabs)
        {
            if (prefab == null || cycle.Contains(prefab))
                continue;

            if (!prefab.TryGetComponent(out NavMeshSurface surface) ||
                !surface.enabled || surface.navMeshData == null)
            {
                Debug.LogWarning($"{prefab.name} needs an enabled NavMeshSurface with baked data on its root.", this);
                continue;
            }

            cycle.Add(prefab);
        }

        if (cycle.Count > 0)
        {
            ShuffleFrom(0);
            ActivateLayout(cycle[0]);
        }
    }

    private void Update()
    {
        if (cycle.Count < 2 || activeLayout == null)
            return;

        secondsRemaining -= Time.deltaTime;

        if (secondsRemaining <= 0f || (Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame))
        {
            RefreshNow();
        }
    }

    [ContextMenu("Refresh Now")]
    public void RefreshNow()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || activeLayout == null || cycle.Count < 2)
            return;

        int nextIndex = currentIndex + 1;
        if (nextIndex == cycle.Count)
        {
            // Keep the first layout of the game at the start of every cycle.
            ShuffleFrom(1);
            nextIndex = 0;
        }

        ActivateLayout(cycle[nextIndex]);
        Debug.Log($"Maze refreshed from '{cycle[currentIndex].name}' to '{cycle[nextIndex].name}'.", this);
        currentIndex = nextIndex;
    }

    private void ActivateLayout(GameObject prefab)
    {
        GameObject previousLayout = activeLayout;
        if (previousLayout != null)
            previousLayout.SetActive(false);

        activeLayout = Instantiate(prefab, transform, false);
        activeLayout.SetActive(true);

        if (previousLayout != null)
            Destroy(previousLayout);

        secondsRemaining = refreshInterval;

        // Both manual and timed refreshes start a fresh countdown.
        nextRefreshTime = Time.time + Mathf.Max(1f, refreshInterval);
        // Notify enemies that the layout has changed
        LayoutChanged?.Invoke(prefab, activeLayout.transform);
    }

    private void ShuffleFrom(int firstIndex)
    {
        for (int i = cycle.Count - 1; i > firstIndex; i--)
        {
            int otherIndex = Random.Range(firstIndex, i + 1);
            GameObject prefab = cycle[i];
            cycle[i] = cycle[otherIndex];
            cycle[otherIndex] = prefab;
        }
    }
}
