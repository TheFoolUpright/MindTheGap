
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

    // Temporary test interface.
    private void OnGUI()
    {
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
}
