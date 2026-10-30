
using System;
using Unity.Netcode;
using UnityEngine;

public class FurnaceNetworkState : NetworkBehaviour
{
    public static FurnaceNetworkState Instance { get; private set; }

    private readonly NetworkVariable<FurnaceNetworkSnapshot>
        furnaceState = new(
            default,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

    // Notifies local components when the state changes.
    public event Action<FurnaceState> OnStateChanged;

    public FurnaceState CurrentState =>
        furnaceState.Value.ToState();

    public override void OnNetworkSpawn()
    {
        Instance = this;

        furnaceState.OnValueChanged += HandleStateChanged;

        Debug.Log(
            $"FurnaceNetworkState spawned. Server: {IsServer}"
        );
    }

    public override void OnNetworkDespawn()
    {
        furnaceState.OnValueChanged -= HandleStateChanged;

        if (Instance == this)
            Instance = null;
    }

    private void HandleStateChanged(
        FurnaceNetworkSnapshot previous,
        FurnaceNetworkSnapshot current)
    {
        Debug.Log(
            $"Furnace synchronized: " +
            $"Cubes {current.totalCubes}/{current.cubeGoal}, " +
            $"Pyramids {current.totalPyramids}/{current.pyramidGoal}"
        );

        OnStateChanged?.Invoke(current.ToState());
    }

    // Called only by the Explorer's local furnace.
    public void PublishState(FurnaceState state)
    {
        if (!IsSpawned || !IsServer)
        {
            Debug.LogWarning(
                "Only the Explorer host can publish furnace state."
            );
            return;
        }

        furnaceState.Value =
            FurnaceNetworkSnapshot.FromState(state);
    }
}
