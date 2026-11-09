
using System;
using Unity.Netcode;
using UnityEngine;

// Where the current refresh is in its lifecycle.
public enum MazeRefreshPhase : byte
{
    // No maze has been selected yet.
    None,

    // The host is replacing the maze. Clients should lock
    // interactions and show the "maze shifting" overlay.
    Preparing,

    // The new maze and its door/light state are in place.
    Ready
}

// One complete update: which maze, which refresh, and its phase.
public struct MazeRefreshSnapshot :
    INetworkSerializable, IEquatable<MazeRefreshSnapshot>
{
    public int LayoutId;
    public int Generation;
    public MazeRefreshPhase Phase;

    public MazeRefreshSnapshot(
        int layoutId,
        int generation,
        MazeRefreshPhase phase)
    {
        LayoutId = layoutId;
        Generation = generation;
        Phase = phase;
    }

    public bool Equals(MazeRefreshSnapshot other)
    {
        return LayoutId == other.LayoutId &&
               Generation == other.Generation &&
               Phase == other.Phase;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref LayoutId);
        serializer.SerializeValue(ref Generation);
        serializer.SerializeValue(ref Phase);
    }

    public override string ToString()
    {
        return $"Layout {LayoutId}, Generation {Generation}, {Phase}";
    }
}

public class MazeRefreshNetworkState : NetworkBehaviour
{
    public static MazeRefreshNetworkState Instance
    {
        get;
        private set;
    }

    // -1 means no maze has been selected yet.
    // Generation 0 means refresh has not started.
    public readonly NetworkVariable<MazeRefreshSnapshot> Current =
        new(
            new MazeRefreshSnapshot(-1, 0, MazeRefreshPhase.None),
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public event Action<MazeRefreshSnapshot> LayoutChanged;

    public override void OnNetworkSpawn()
    {
        Instance = this;

        Current.OnValueChanged += OnCurrentChanged;

        Debug.Log(
            $"MazeRefreshNetworkState spawned. " +
            $"Server: {IsServer}, {Current.Value}"
        );
    }

    public override void OnNetworkDespawn()
    {
        Current.OnValueChanged -= OnCurrentChanged;

        if (Instance == this)
            Instance = null;
    }

    // Host only. Starts a new generation for the given layout.
    // Returns the new generation, or -1 if the call was rejected.
    public int BeginRefresh(int layoutId)
    {
        if (!IsSpawned || !IsServer)
        {
            Debug.LogWarning(
                "Only the host can start maze refreshes."
            );
            return -1;
        }

        if (layoutId < 0)
        {
            Debug.LogError(
                $"Invalid maze layout ID: {layoutId}"
            );
            return -1;
        }

        int generation = Current.Value.Generation + 1;

        Current.Value = new MazeRefreshSnapshot(
            layoutId,
            generation,
            MazeRefreshPhase.Preparing
        );

        return generation;
    }

    // Host only. Marks the current generation as finished.
    // Call this at least one network tick after the door and
    // light state was reset, so clients receive that first.
    public void CompleteRefresh()
    {
        if (!IsSpawned || !IsServer)
            return;

        MazeRefreshSnapshot current = Current.Value;

        if (current.Phase != MazeRefreshPhase.Preparing)
            return;

        current.Phase = MazeRefreshPhase.Ready;
        Current.Value = current;
    }

    private void OnCurrentChanged(
        MazeRefreshSnapshot previous,
        MazeRefreshSnapshot current)
    {
        Debug.Log(
            $"[Maze Refresh] {current} (Server: {IsServer})"
        );

        LayoutChanged?.Invoke(current);
    }

    // Useful for listeners that connect after
    // the initial network state has arrived.
    public bool TryGetCurrent(out MazeRefreshSnapshot snapshot)
    {
        snapshot = Current.Value;

        return IsSpawned && snapshot.Generation > 0;
    }
}
