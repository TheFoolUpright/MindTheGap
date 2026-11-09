
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeDoorNetworkBinding : MonoBehaviour
{
    [Header("Generated Maze or Map")]
    [SerializeField] private Transform generatedRoot;

    private MazeNetworkState mazeNetworkState;

    private readonly Dictionary<string, NavigatorDoorNode>
        navigatorDoors = new();

    private readonly Dictionary<string, ExplorerDoor>
        explorerDoors = new();

    private Coroutine connectionCoroutine;

    private void OnEnable()
    {
        CacheDoors();
        connectionCoroutine = StartCoroutine(ConnectWhenReady());
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
            mazeNetworkState.DoorStateChanged -= ApplyDoorState;
            mazeNetworkState = null;
        }
    }

    private void CacheDoors()
    {
        navigatorDoors.Clear();
        explorerDoors.Clear();

        if (generatedRoot == null)
        {
            Debug.LogError("Generated Root is not assigned.", this);
            return;
        }

        // Find Navigator UI doors.
        foreach (NavigatorDoorNode door in
            generatedRoot.GetComponentsInChildren<NavigatorDoorNode>(true))
        {
            if (!navigatorDoors.TryAdd(door.DoorId, door))
                Debug.LogWarning(
                    $"Duplicate Navigator door ID: {door.DoorId}", this);
        }

        // Find Explorer physical doors.
        foreach (ExplorerDoor door in
            generatedRoot.GetComponentsInChildren<ExplorerDoor>(true))
        {
            if (!explorerDoors.TryAdd(door.DoorId, door))
                Debug.LogWarning(
                    $"Duplicate Explorer door ID: {door.DoorId}", this);
        }

     
    }

    private IEnumerator ConnectWhenReady()
    {
        // GameSession and the role scenes load separately.
        // Wait until the shared network state is available.
        while (mazeNetworkState == null)
        {
            MazeNetworkState found =
                FindAnyObjectByType<MazeNetworkState>();

            if (found != null && found.IsSpawned)
            {
                mazeNetworkState = found;
                break;
            }

            yield return new WaitForSeconds(0.2f);
        }

        // Listen for future changes.
        mazeNetworkState.DoorStateChanged += ApplyDoorState;

        // Apply the current states immediately.
        mazeNetworkState.ApplyCurrentDoorStates(ApplyDoorState);
    }

    private void ApplyDoorState(string doorId, MazeDoorState state)
    {
        // Update the Navigator's map representation.
        if (navigatorDoors.TryGetValue(
            doorId, out NavigatorDoorNode navigatorDoor))
        {
            navigatorDoor.SetState(state);
        }

        // Update the Explorer's physical representation.
        if (explorerDoors.TryGetValue(
            doorId, out ExplorerDoor explorerDoor))
        {
            explorerDoor.SetState(state);
        }
    }

    // Called by the refresh system: point at the new maze or map
    // and apply the current network state to its doors.
    public void SetRoot(Transform newRoot)
    {
        generatedRoot = newRoot;
        RefreshDoorReferences();
    }

    // Useful when replacing the generated maze during a refresh.
    public void RefreshDoorReferences()
    {
        CacheDoors();

        if (mazeNetworkState != null && mazeNetworkState.IsSpawned)
            mazeNetworkState.ApplyCurrentDoorStates(ApplyDoorState);
    }
}
