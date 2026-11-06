
using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

// One door's state, synchronized across the network.
public struct NetworkDoorState : INetworkSerializable,
    IEquatable<NetworkDoorState>
{
    public FixedString32Bytes DoorId;
    public MazeDoorState State;

    public NetworkDoorState(string doorId, MazeDoorState state)
    {
        DoorId = new FixedString32Bytes(doorId);
        State = state;
    }

    public bool Equals(NetworkDoorState other)
    {
        return DoorId.Equals(other.DoorId) &&
               State == other.State;
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref DoorId);
        serializer.SerializeValue(ref State);
    }
}

public class MazeNetworkState : NetworkBehaviour
{
    [Header("Initial Maze")]
    [SerializeField] private MazeLayoutData initialLayout;

    [Header("Door Settings")]
    [SerializeField, Min(0.1f)]
    private float doorOpenSeconds = 5f;

    private NetworkList<NetworkDoorState> doors;

    // Host-only countdowns. These are not networked.
    private readonly Dictionary<string, float> closeTimes = new();
    private readonly List<string> expiredDoors = new();

    // Other components can subscribe to state changes.
    public event Action<string, MazeDoorState> DoorStateChanged;

    private void Awake()
    {
        doors = new NetworkList<NetworkDoorState>();
    }

    public override void OnNetworkSpawn()
    {
        doors.OnListChanged += OnDoorListChanged;

        if (IsServer)
            InitializeDoors();

        Debug.Log(
            $"MazeNetworkState spawned. " +
            $"Server: {IsServer}, Doors: {doors.Count}",
            this
        );
    }

    public override void OnNetworkDespawn()
    {
        doors.OnListChanged -= OnDoorListChanged;
        closeTimes.Clear();
    }

    // Host creates the initial door list from the Tiled layout.
    private void InitializeDoors()
    {
        if (!IsServer)
            return;

        if (initialLayout == null || !initialLayout.IsConfigured)
        {
            Debug.LogError("MazeNetworkState needs a valid layout.", this);
            return;
        }

        doors.Clear();
        closeTimes.Clear();

        // Vertical doors.
        for (int y = 0; y < initialLayout.Height; y++)
        {
            for (int x = 0; x <= initialLayout.Width; x++)
            {
                if (initialLayout.GetVerticalEdge(x, y) !=
                    MazeEdgeType.Door)
                    continue;

                doors.Add(new NetworkDoorState(
                    $"V_{x}_{y}",
                    MazeDoorState.Closed
                ));
            }
        }

        // Horizontal doors.
        for (int y = 0; y <= initialLayout.Height; y++)
        {
            for (int x = 0; x < initialLayout.Width; x++)
            {
                if (initialLayout.GetHorizontalEdge(x, y) !=
                    MazeEdgeType.Door)
                    continue;

                doors.Add(new NetworkDoorState(
                    $"H_{x}_{y}",
                    MazeDoorState.Closed
                ));
            }
        }

        Debug.Log(
            $"Host initialized {doors.Count} networked doors.",
            this
        );
    }

    // Called by the Navigator after a valid code.
    public void RequestToggleDoor(string doorId)
    {
        if (!IsSpawned)
        {
            Debug.LogWarning("MazeNetworkState is not spawned.", this);
            return;
        }

        if (string.IsNullOrEmpty(doorId))
            return;

        // The RPC sends the request to the host.
        RequestToggleDoorRpc(new FixedString32Bytes(doorId));
    }

    [Rpc(SendTo.Server)]
    private void RequestToggleDoorRpc(FixedString32Bytes doorId)
    {
        if (!IsServer)
            return;

        for (int i = 0; i < doors.Count; i++)
        {
            NetworkDoorState door = doors[i];

            if (!door.DoorId.Equals(doorId))
                continue;

            MazeDoorState nextState =
                door.State == MazeDoorState.Closed
                    ? MazeDoorState.Open
                    : MazeDoorState.Closed;

            SetDoorStateOnServer(doorId, nextState);
            return;
        }

        Debug.LogWarning($"Unknown door ID: {doorId}", this);
    }

    // Only the host changes authoritative door states.
    private void SetDoorStateOnServer(
        FixedString32Bytes doorId,
        MazeDoorState state)
    {
        if (!IsServer)
            return;

        for (int i = 0; i < doors.Count; i++)
        {
            NetworkDoorState door = doors[i];

            if (!door.DoorId.Equals(doorId))
                continue;

            if (door.State != state)
            {
                door.State = state;
                doors[i] = door;
            }

            string id = doorId.ToString();

            if (state == MazeDoorState.Open)
                closeTimes[id] = Time.time + doorOpenSeconds;
            else
                closeTimes.Remove(id);

            return;
        }
    }

    // Automatic closing is processed only by the host.
    private void Update()
    {
        if (!IsSpawned || !IsServer || closeTimes.Count == 0)
            return;

        expiredDoors.Clear();

        foreach (var entry in closeTimes)
        {
            if (Time.time >= entry.Value)
                expiredDoors.Add(entry.Key);
        }

        foreach (string id in expiredDoors)
        {
            SetDoorStateOnServer(
                new FixedString32Bytes(id),
                MazeDoorState.Closed
            );
        }
    }

    // Called when the NetworkList changes locally or remotely.
    private void OnDoorListChanged(
        NetworkListEvent<NetworkDoorState> change)
    {
        if (change.Type !=
            NetworkListEvent<NetworkDoorState>.EventType.Value)
            return;

        NetworkDoorState door = change.Value;

        DoorStateChanged?.Invoke(
            door.DoorId.ToString(),
            door.State
        );

        // Temporary networking verification.
        Debug.Log(
            $"[Maze Network] Door {door.DoorId} -> {door.State} " +
            $"(Server: {IsServer})",
            this
        );
    }

    // Read an individual door's current networked state.
    public bool TryGetDoorState(
        string doorId,
        out MazeDoorState state)
    {
        state = MazeDoorState.Closed;

        if (!IsSpawned)
            return false;

        for (int i = 0; i < doors.Count; i++)
        {
            if (doors[i].DoorId.ToString() != doorId)
                continue;

            state = doors[i].State;
            return true;
        }

        return false;
    }

    // Useful when a scene loads after network state is initialized.
    public void ApplyCurrentDoorStates(
        Action<string, MazeDoorState> apply)
    {
        if (!IsSpawned || apply == null)
            return;

        foreach (NetworkDoorState door in doors)
            apply(door.DoorId.ToString(), door.State);
    }

    // Temporary test, callable through the Inspector in Play Mode.
    [ContextMenu("Debug/Toggle First Network Door")]
    private void DebugToggleFirstDoor()
    {
        if (!IsSpawned || doors.Count == 0)
        {
            Debug.LogWarning("No networked doors available.", this);
            return;
        }

        RequestToggleDoor(doors[0].DoorId.ToString());
    }
}
