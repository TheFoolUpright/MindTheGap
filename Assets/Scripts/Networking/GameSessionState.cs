using System.Collections.Generic;
using System;
using Unity.Netcode;
using UnityEngine;

// The overall phase of a Mind the Gap game session.
public enum GamePhase : byte
{
    Initializing,
    Playing,
    Finished
}

public class GameSessionState : NetworkBehaviour
{
    public static GameSessionState Instance { get; private set; }

    // The host controls the shared game phase.
    // Both players are allowed to read it.
    public readonly NetworkVariable<GamePhase> Phase = new(
        GamePhase.Initializing,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server
    );

    // Other local scripts can subscribe to changes.
    public event Action<GamePhase> OnPhaseChanged;

    // The host tracks which players loaded their role scenes.
    private readonly HashSet<ulong> readyClients = new();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    [SerializeField] private bool showDebugUI = false;
#endif

    public override void OnNetworkSpawn()
    {
        Instance = this;

        // Listen for future updates.
        Phase.OnValueChanged += HandlePhaseChanged;

        Debug.Log(
            $"GameSessionState spawned. " +
            $"Server: {IsServer}, " +
            $"Client: {IsClient}, " +
            $"Phase: {Phase.Value}"
        );

        // Notify listeners of the initial state.
        OnPhaseChanged?.Invoke(Phase.Value);
    }

    public override void OnNetworkDespawn()
    {
        Phase.OnValueChanged -= HandlePhaseChanged;

        if (Instance == this)
        {
            Instance = null;
        }

        readyClients.Clear();
    }

    private void HandlePhaseChanged(
        GamePhase previous,
        GamePhase current)
    {
        Debug.Log(
            $"Game phase changed: {previous} -> {current}"
        );

        OnPhaseChanged?.Invoke(current);
    }

    // Only the Explorer host can change this state.
    public void SetPhase(GamePhase newPhase)
    {
        if (!IsSpawned || !IsServer)
        {
            Debug.LogWarning(
                "Only the active server can change game phase."
            );
            return;
        }

        Phase.Value = newPhase;
    }

#if UNITY_EDITOR || DEVELOPMENT_BUILD

    private void OnGUI()
    {
        if (!showDebugUI)
            return;

        if (!IsSpawned) return;

        GUILayout.BeginArea(
            new Rect(20, 140, 350, 135),
            GUI.skin.box
        );

        GUILayout.Label("NETWORKED GAME STATE");
        GUILayout.Label($"Current phase: {Phase.Value}");

        if (IsServer)
        {
            if (GUILayout.Button("Test: Start Playing"))
            {
                SetPhase(GamePhase.Playing);
            }

            if (GUILayout.Button("Test: Finish Game"))
            {
                SetPhase(GamePhase.Finished);
            }
        }

        GUILayout.EndArea();
    }

#endif


    public void ReportLocalRoleSceneLoaded()
    {
        if (!IsSpawned)
            return;

        // The Explorer host can report directly.
        if (IsServer)
        {
            MarkClientSceneLoaded(NetworkManager.LocalClientId);
        }
        else
        {
            // The Navigator tells the host it has loaded.
            ReportRoleSceneLoadedRpc();
        }
    }

    [Rpc(SendTo.Server)]
    private void ReportRoleSceneLoadedRpc(RpcParams rpcParams = default)
    {
        MarkClientSceneLoaded(
            rpcParams.Receive.SenderClientId
        );
    }

    private void MarkClientSceneLoaded(ulong clientId)
    {
        if (!IsServer || Phase.Value != GamePhase.Initializing)
            return;

        // Verify the reporting player is connected.
        bool connected = false;

        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            if (id == clientId)
            {
                connected = true;
                break;
            }
        }

        if (!connected)
            return;

        if (readyClients.Add(clientId))
        {
            Debug.Log(
                $"Role scene ready for client {clientId}."
            );
        }

        // Mind the Gap currently requires exactly two players.
        if (NetworkManager.ConnectedClientsIds.Count != 2)
            return;

        // Both connected players must be ready.
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            if (!readyClients.Contains(id))
                return;
        }

        Debug.Log("Both role scenes loaded. Starting gameplay.");

        SetPhase(GamePhase.Playing);
    }



}
