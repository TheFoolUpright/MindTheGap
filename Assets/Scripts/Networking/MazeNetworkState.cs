
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

public struct NetworkLightState : INetworkSerializable,
    IEquatable<NetworkLightState>
{
    public int LightId;
    public MazeLightState State;

    public NetworkLightState(int lightId, MazeLightState state)
    {
        LightId = lightId;
        State = state;
    }

    public bool Equals(NetworkLightState other)
    {
        return LightId == other.LightId &&
               State == other.State;
    }

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref LightId);
        serializer.SerializeValue(ref State);
    }
}


public struct NetworkEnemyPose : INetworkSerializable, IEquatable<NetworkEnemyPose>
{
    public int Id;
    public int Generation;
    public Vector2 CellPosition;
    public float FacingDegrees;

    public bool Equals(NetworkEnemyPose other)
    {
        return Id == other.Id && Generation == other.Generation &&
               CellPosition.Equals(other.CellPosition) && FacingDegrees.Equals(other.FacingDegrees);
    }

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref Id);
        serializer.SerializeValue(ref Generation);
        serializer.SerializeValue(ref CellPosition);
        serializer.SerializeValue(ref FacingDegrees);
    }
}

public class MazeNetworkState : NetworkBehaviour
{
    // Optional. Only used by scenes without MazeRefresh.
    // With MazeRefresh, the host calls ResetForLayout instead.
    [Header("Fallback Maze (scenes without MazeRefresh)")]
    [SerializeField] private MazeLayoutData initialLayout;

    // The refresh generation the door and light lists belong to.
    // Written after the lists, so a client that sees this value
    // also has the matching lists.
    private readonly NetworkVariable<int> stateGeneration =
        new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public int StateGeneration => stateGeneration.Value;

    [Header("Door Settings")]
    [SerializeField, Min(0.1f)]
    private float doorOpenSeconds = 5f;

    private NetworkList<NetworkDoorState> doors;

    // Host-only countdowns. These are not networked.
    private readonly Dictionary<string, float> closeTimes = new();
    private readonly List<string> expiredDoors = new();

    // Other components can subscribe to state changes.
    public event Action<string, MazeDoorState> DoorStateChanged;


    [Header("Light Settings")]
    [SerializeField, Min(0.1f)]
    private float lightOnSeconds = 5f;

    private NetworkList<NetworkLightState> lights;

    // Host-only timers.
    private readonly Dictionary<int, float> lightOffTimes = new();
    private readonly List<int> expiredLightIds = new();

    public event Action<int, MazeLightState> LightStateChanged;



    [Header("Explorer Tracking")]
    private readonly NetworkVariable<Vector2> explorerMazePosition =
        new NetworkVariable<Vector2>(
            Vector2.zero,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public Vector2 ExplorerMazePosition => explorerMazePosition.Value;

    private readonly NetworkVariable<bool> explorerPositionAvailable =
        new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public event Action<Vector2> ExplorerPositionChanged;
    public bool HasExplorerPosition => explorerPositionAvailable.Value;


    private readonly NetworkVariable<float> explorerFacingDegrees =
        new NetworkVariable<float>(
            0f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    public float ExplorerFacingDegrees => explorerFacingDegrees.Value;

    public event Action<float> ExplorerFacingChanged;






    private NetworkList<NetworkEnemyPose> enemyPoses;

    public int EnemyPoseCount => enemyPoses.Count;

    public NetworkEnemyPose GetEnemyPose(int index)
    {
        return enemyPoses[index];
    }

    public void PublishEnemyPoses(
        int generation, List<NetworkEnemyPose> poses, float minimumMovement, float minimumTurnDegrees)
    {
        if (!IsSpawned || !IsServer || generation <= 0 || generation != StateGeneration)
            return;

        for (int i = enemyPoses.Count - 1; i >= 0; i--)
        {
            if (FindEnemyPose(poses, enemyPoses[i].Id) < 0)
                enemyPoses.RemoveAt(i);
        }

        foreach (NetworkEnemyPose pose in poses)
        {
            int index = -1;
            for (int i = 0; i < enemyPoses.Count; i++)
            {
                if (enemyPoses[i].Id == pose.Id)
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                enemyPoses.Add(pose);
                continue;
            }

            NetworkEnemyPose previous = enemyPoses[index];
            if (!pose.Equals(previous) &&
                ((pose.CellPosition - previous.CellPosition).sqrMagnitude >= minimumMovement * minimumMovement ||
                Mathf.Abs(Mathf.DeltaAngle(previous.FacingDegrees, pose.FacingDegrees)) >= minimumTurnDegrees))
            {
                enemyPoses[index] = pose;
            }
        }
    }

    private static int FindEnemyPose(List<NetworkEnemyPose> poses, int id)
    {
        for (int i = 0; i < poses.Count; i++)
        {
            if (poses[i].Id == id)
                return i;
        }

        return -1;
    }

    private void Awake()
    {
        doors = new NetworkList<NetworkDoorState>();
        lights = new NetworkList<NetworkLightState>();
        enemyPoses = new NetworkList<NetworkEnemyPose>();
    }

    public override void OnNetworkSpawn()
    {
        explorerMazePosition.OnValueChanged += OnExplorerPositionChanged;
        explorerPositionAvailable.OnValueChanged += OnExplorerPositionAvailabilityChanged;
        explorerFacingDegrees.OnValueChanged += OnExplorerFacingChanged;

        doors.OnListChanged += OnDoorListChanged;
        lights.OnListChanged += OnLightListChanged;

        if (IsServer && initialLayout != null)
        {
            InitializeDoors(initialLayout);
            InitializeLights(initialLayout);
        }

        Debug.Log(
            $"MazeNetworkState spawned. " +
            $"Server: {IsServer}, Doors: {doors.Count}, Lights: {lights.Count}",
            this
        );
    }

    public override void OnNetworkDespawn()
    {
        explorerMazePosition.OnValueChanged -= OnExplorerPositionChanged;
        explorerPositionAvailable.OnValueChanged -= OnExplorerPositionAvailabilityChanged;
        explorerFacingDegrees.OnValueChanged -= OnExplorerFacingChanged;

        doors.OnListChanged -= OnDoorListChanged;
        closeTimes.Clear();

        lights.OnListChanged -= OnLightListChanged;
        lightOffTimes.Clear();
    }

    // Host only. Replaces every door and light with the ones in the
    // new layout: doors Closed, lights Off (Broken lights are restored),
    // and all host timers cancelled.
    public void ResetForLayout(MazeLayoutData layout, int generation)
    {
        if (!IsSpawned || !IsServer)
        {
            Debug.LogWarning(
                "Only the host can reset the maze state.",
                this
            );
            return;
        }

        if (layout == null || !layout.IsConfigured)
        {
            Debug.LogError(
                "ResetForLayout needs a valid layout.",
                this
            );
            return;
        }

        InitializeDoors(layout);
        InitializeLights(layout);

        enemyPoses.Clear();

        // Written last: clients use it to know the lists are current.
        stateGeneration.Value = generation;

        Debug.Log(
            $"[Maze Network] Reset for layout {layout.LayoutId}, " +
            $"generation {generation}.",
            this
        );
    }

    // Host creates the door list from the Tiled layout.
    private void InitializeDoors(MazeLayoutData layout)
    {
        if (!IsServer)
            return;

        if (layout == null || !layout.IsConfigured)
        {
            Debug.LogError("MazeNetworkState needs a valid layout.", this);
            return;
        }

        doors.Clear();
        closeTimes.Clear();
        expiredDoors.Clear();

        // Vertical doors.
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x <= layout.Width; x++)
            {
                if (layout.GetVerticalEdge(x, y) !=
                    MazeEdgeType.Door)
                    continue;

                doors.Add(new NetworkDoorState(
                    $"V_{x}_{y}",
                    MazeDoorState.Closed
                ));
            }
        }

        // Horizontal doors.
        for (int y = 0; y <= layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                if (layout.GetHorizontalEdge(x, y) !=
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


    private void InitializeLights(MazeLayoutData layout)
    {
        if (!IsServer)
            return;

        if (layout == null)
        {
            Debug.LogError(
                "Cannot initialize lights: no maze layout assigned.",
                this
            );
            return;
        }

        lights.Clear();
        lightOffTimes.Clear();
        expiredLightIds.Clear();

        foreach (MazeLightData light in layout.Lights)
        {
            lights.Add(new NetworkLightState(
                light.id,
                MazeLightState.Off
            ));
        }

        Debug.Log(
            $"Host initialized {lights.Count} networked lights.",
            this
        );
    }


    public void RequestToggleLight(int lightId)
    {
        if (!IsSpawned)
            return;

        RequestToggleLightRpc(lightId);
    }

    public void RequestOverloadLight(int lightId)
    {
        if (!IsSpawned)
            return;

        RequestOverloadLightRpc(lightId);
    }

    [Rpc(SendTo.Server)]
    private void RequestToggleLightRpc(int lightId)
    {
        if (!IsServer)
            return;

        foreach (NetworkLightState light in lights)
        {
            if (light.LightId != lightId)
                continue;

            // Broken lights cannot be toggled.
            if (light.State == MazeLightState.Broken)
                return;

            MazeLightState nextState =
                light.State == MazeLightState.On
                    ? MazeLightState.Off
                    : MazeLightState.On;

            SetLightStateOnServer(lightId, nextState);
            return;
        }

        Debug.LogWarning($"Unknown light ID: {lightId}", this);
    }

    [Rpc(SendTo.Server)]
    private void RequestOverloadLightRpc(int lightId)
    {
        if (!IsServer)
            return;

        SetLightStateOnServer(
            lightId,
            MazeLightState.Broken
        );
    }


    private void SetLightStateOnServer(
        int lightId,
        MazeLightState newState)
    {
        if (!IsServer)
            return;

        for (int i = 0; i < lights.Count; i++)
        {
            NetworkLightState light = lights[i];

            if (light.LightId != lightId)
                continue;

            // Broken lights remain broken until a maze refresh.
            if (light.State == MazeLightState.Broken &&
                newState != MazeLightState.Broken)
            {
                return;
            }

            if (light.State != newState)
            {
                light.State = newState;
                lights[i] = light;
            }

            // Only On lights need an automatic shutoff.
            if (newState == MazeLightState.On)
            {
                lightOffTimes[lightId] =
                    Time.time + lightOnSeconds;
            }
            else
            {
                // Off and Broken both cancel the countdown.
                lightOffTimes.Remove(lightId);
            }

            return;
        }

        Debug.LogWarning(
            $"Cannot change unknown light {lightId}.",
            this
        );
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


    private void LateUpdate()
    {
        if (!IsSpawned || !IsServer || lightOffTimes.Count == 0)
            return;

        expiredLightIds.Clear();

        foreach (var entry in lightOffTimes)
        {
            if (Time.time >= entry.Value)
                expiredLightIds.Add(entry.Key);
        }

        foreach (int lightId in expiredLightIds)
        {
            SetLightStateOnServer(
                lightId,
                MazeLightState.Off
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



    // position is in maze cell units: (0,0) = outer corner of cell (0,0),
    // cell (x, y) centre = (x + 0.5, y + 0.5). Independent of cell size in metres.
    public void PublishExplorerPosition(Vector2 position)
    {
        if (!IsSpawned || !IsServer)
            return;

        explorerMazePosition.Value = position;
        explorerPositionAvailable.Value = true;
    }

    public void PublishExplorerFacing(float degrees)
    {
        if (!IsSpawned || !IsServer)
            return;

        explorerFacingDegrees.Value = degrees;
    }

    private void OnExplorerPositionChanged(
    Vector2 previous,
    Vector2 current)
    {
        ExplorerPositionChanged?.Invoke(current);
    }

    private void OnExplorerPositionAvailabilityChanged(
    bool previous,
    bool current)
    {
        ExplorerPositionChanged?.Invoke(ExplorerMazePosition);
    }

    private void OnExplorerFacingChanged(float previous, float current)
    {
        ExplorerFacingChanged?.Invoke(current);
    }

    [ContextMenu("Debug/Print Explorer Position")]
    private void DebugPrintExplorerPosition()
    {
        Debug.Log(
            $"[Explorer Tracking] " +
            $"Server: {IsServer}, " +
            $"Position: {ExplorerMazePosition}",
            this
        );
    }


    private void OnLightListChanged(
        NetworkListEvent<NetworkLightState> change)
    {
        if (change.Type !=
            NetworkListEvent<NetworkLightState>.EventType.Value)
            return;

        NetworkLightState light = change.Value;

        LightStateChanged?.Invoke(
            light.LightId,
            light.State
        );

        Debug.Log(
            $"[Maze Network] Light {light.LightId} -> {light.State} " +
            $"(Server: {IsServer})",
            this
        );
    }

    public void ApplyCurrentLightStates(
        Action<int, MazeLightState> apply)
    {
        if (!IsSpawned || apply == null)
            return;

        foreach (NetworkLightState light in lights)
            apply(light.LightId, light.State);
    }



    [ContextMenu("Debug/Toggle First Network Light")]
    private void DebugToggleFirstLight()
    {
        if (!IsSpawned || lights.Count == 0)
        {
            Debug.LogWarning("No networked lights found.", this);
            return;
        }

        RequestToggleLight(lights[0].LightId);
    }

    [ContextMenu("Debug/Overload First Network Light")]
    private void DebugOverloadFirstLight()
    {
        if (!IsSpawned || lights.Count == 0)
        {
            Debug.LogWarning("No networked lights found.", this);
            return;
        }

        RequestOverloadLight(lights[0].LightId);
    }

}
