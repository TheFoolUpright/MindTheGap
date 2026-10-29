
using Unity.Netcode;
using Unity.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class Lobby : NetworkBehaviour
{
    public static Lobby Instance { get; private set; }

    public NetworkList<LobbyEntry> Players;

    private void Awake()
    {
        Instance = this;
        Players = new NetworkList<LobbyEntry>();
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer) return;

        NetworkManager.OnClientConnectedCallback += OnClientJoined;
        NetworkManager.OnClientDisconnectCallback += OnClientLeft;

        // The host may already be connected when this
        // scene object becomes network-spawned.
        foreach (ulong id in NetworkManager.ConnectedClientsIds)
        {
            OnClientJoined(id);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer) return;

        NetworkManager.OnClientConnectedCallback -= OnClientJoined;
        NetworkManager.OnClientDisconnectCallback -= OnClientLeft;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    // SERVER: register a connected player.
    private void OnClientJoined(ulong clientId)
    {
        if (!IsServer) return;

        if (FindPlayerIndex(clientId) >= 0)
            return;

        // Mind the Gap only supports two players.
        if (Players.Count >= 2)
        {
            Debug.LogWarning("Lobby is full.");

            NetworkManager.DisconnectClient(clientId);
            return;
        }

        PlayerRole role =
            clientId == NetworkManager.ServerClientId
                ? PlayerRole.Explorer
                : PlayerRole.Navigator;

        LobbyEntry entry = new LobbyEntry
        {
            ClientId = clientId,
            Name = role == PlayerRole.Explorer
                ? new FixedString32Bytes("Explorer")
                : new FixedString32Bytes("Navigator"),
            Role = role,
            Ready = false
        };

        Players.Add(entry);

        Debug.Log($"{role} joined the lobby.");
    }

    // SERVER: remove a disconnected player.
    private void OnClientLeft(ulong clientId)
    {
        if (!IsServer) return;

        int index = FindPlayerIndex(clientId);

        if (index >= 0)
        {
            Players.RemoveAt(index);
            Debug.Log($"Client {clientId} left the lobby.");
        }
    }

    // CLIENT: request a Ready status change.
    public void ToggleReady()
    {
        if (!IsSpawned) return;

        ToggleReadyRpc();
    }

    // SERVER: process the Ready request.
    [Rpc(SendTo.Server)]
    private void ToggleReadyRpc(RpcParams rpcParams = default)
    {
        ulong senderId = rpcParams.Receive.SenderClientId;

        int index = FindPlayerIndex(senderId);

        if (index < 0) return;

        LobbyEntry entry = Players[index];

        entry.Ready = !entry.Ready;

        // Assigning the modified entry back updates
        // the NetworkList on all connected clients.
        Players[index] = entry;

        Debug.Log($"{entry.Role} ready: {entry.Ready}");
    }

    public bool CanStartGame()
    {
        if (!IsServer) return false;

        if (Players.Count != 2) return false;

        bool hasExplorer = false;
        bool hasNavigator = false;

        foreach (LobbyEntry player in Players)
        {
            if (!player.Ready) return false;

            if (player.Role == PlayerRole.Explorer)
                hasExplorer = true;

            if (player.Role == PlayerRole.Navigator)
                hasNavigator = true;
        }

        return hasExplorer && hasNavigator;
    }

    public bool TryGetLocalPlayer(out LobbyEntry entry)
    {
        ulong localId = NetworkManager.LocalClientId;

        foreach (LobbyEntry player in Players)
        {
            if (player.ClientId == localId)
            {
                entry = player;
                return true;
            }
        }

        entry = default;
        return false;
    }

    private int FindPlayerIndex(ulong clientId)
    {
        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].ClientId == clientId)
                return i;
        }

        return -1;
    }


    public void StartGame()
    {
        if (!IsServer)
        {
            Debug.LogWarning(
                "Only the Explorer host can start the game."
            );
            return;
        }

        if (!CanStartGame())
        {
            Debug.LogWarning(
                "Both players must be ready before starting."
            );
            return;
        }

        Debug.Log("Starting Mind the Gap game session...");

        SceneEventProgressStatus result =
            NetworkManager.SceneManager.LoadScene(
                "GameSession",
                LoadSceneMode.Single
            );

        if (result != SceneEventProgressStatus.Started)
        {
            Debug.LogError(
                $"Failed to start scene transition: {result}"
            );
        }
    }

}
