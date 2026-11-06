
using System;
using System.Threading.Tasks;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

// Unity Gaming Services
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;

public class LobbyManager : MonoBehaviour
{
    [Header("Local Network")]
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;

    [Header("Network Manager")]
    [SerializeField] private NetworkManager networkManager;

    private UnityTransport transport;

    // One Explorer host + one Navigator client.
    private const int MaxRelayClients = 1;

    // Secure WebSockets for Relay.
    private const string RelayConnectionType = "wss";

    // Read by LobbyHUD.
    public string RelayJoinCode { get; private set; } = "";
    public string StatusMessage { get; private set; } = "";
    public bool IsConnecting { get; private set; }

    private void Awake()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
            networkManager =
                FindFirstObjectByType<NetworkManager>();

        if (networkManager == null)
        {
            Debug.LogError(
                "LobbyManager: No NetworkManager found!");
            return;
        }

        transport =
            networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError(
                "LobbyManager: UnityTransport not found!");
        }
    }

    // ============================================
    // LOCAL NETWORK
    // ============================================

    public void SetServerAddress(string address)
    {
        serverAddress = address.Trim();
    }

    public void StartExplorerHost()
    {
        if (!CanStartConnection()) return;
        if (!CanUseLAN()) return;

        RelayJoinCode = "";

        transport.UseWebSockets = false;

        transport.SetConnectionData(
            "127.0.0.1",
            port,
            "0.0.0.0"
        );

        if (networkManager.StartHost())
        {
            StatusMessage = "LAN host started.";
            Debug.Log(StatusMessage);
        }
        else
        {
            StatusMessage = "Failed to start LAN host.";
            Debug.LogError(StatusMessage);
        }
    }

    public void JoinAsNavigator()
    {
        if (!CanStartConnection()) return;
        if (!CanUseLAN()) return;

        if (string.IsNullOrWhiteSpace(serverAddress))
        {
            StatusMessage = "Enter a host IP address.";
            return;
        }

        RelayJoinCode = "";

        transport.UseWebSockets = false;
        transport.SetConnectionData(serverAddress, port);

        if (networkManager.StartClient())
        {
            StatusMessage =
                $"Connecting to {serverAddress}:{port}...";
            Debug.Log(StatusMessage);
        }
        else
        {
            StatusMessage = "Failed to start LAN client.";
            Debug.LogError(StatusMessage);
        }
    }

    // ============================================
    // UNITY RELAY
    // ============================================

    // Initialize Unity Services and sign in.
    // Each computer signs in independently.
    private async Task InitializeRelayServices()
    {
        await UnityServices.InitializeAsync();

        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance
                .SignInAnonymouslyAsync();
        }
    }

    // Called by the Explorer's Host button.
    public async void StartExplorerHostRelay()
    {
        if (!CanStartConnection()) return;

        IsConnecting = true;
        RelayJoinCode = "";
        StatusMessage = "Initializing Unity Services...";

        try
        {
            await InitializeRelayServices();

            StatusMessage = "Creating Relay allocation...";

            // Reserve one remote player connection.
            var allocation =
                await RelayService.Instance
                    .CreateAllocationAsync(MaxRelayClients);

            // Generate the code for the Navigator.
            string joinCode =
                await RelayService.Instance
                    .GetJoinCodeAsync(allocation.AllocationId);

            // Configure Unity Transport for Relay.
            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(
                    allocation,
                    RelayConnectionType
                )
            );

            transport.UseWebSockets = true;

            // Start NGO only after Relay is configured.
            if (networkManager.StartHost())
            {
                RelayJoinCode = joinCode;
                StatusMessage = "Relay host started.";

                Debug.Log(
                    $"Relay host started. Join code: {joinCode}");
            }
            else
            {
                StatusMessage =
                    "Failed to start Relay host.";
                Debug.LogError(StatusMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"Relay host error: {ex.Message}";

            Debug.LogException(ex);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    // Called by the Navigator's Join button.
    public async void JoinAsNavigatorRelay(string joinCode)
    {
        if (!CanStartConnection()) return;

        joinCode = joinCode.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(joinCode))
        {
            StatusMessage = "Enter a Relay join code.";
            return;
        }

        IsConnecting = true;
        StatusMessage = "Initializing Unity Services...";

        try
        {
            await InitializeRelayServices();

            StatusMessage = "Joining Relay allocation...";

            // Find the host's Relay allocation.
            var allocation =
                await RelayService.Instance
                    .JoinAllocationAsync(joinCode);

            // Configure Unity Transport for Relay.
            transport.SetRelayServerData(
                AllocationUtils.ToRelayServerData(
                    allocation,
                    RelayConnectionType
                )
            );

            transport.UseWebSockets = true;

            if (networkManager.StartClient())
            {
                StatusMessage = "Connecting through Relay...";
                Debug.Log(StatusMessage);
            }
            else
            {
                StatusMessage =
                    "Failed to start Relay client.";
                Debug.LogError(StatusMessage);
            }
        }
        catch (Exception ex)
        {
            StatusMessage =
                $"Relay join error: {ex.Message}";

            Debug.LogException(ex);
        }
        finally
        {
            IsConnecting = false;
        }
    }

    // ============================================
    // SHARED CONNECTION HELPERS
    // ============================================

    private bool CanStartConnection()
    {
        if (!ValidateNetworkSetup()) return false;

        if (IsConnecting || networkManager.IsListening)
        {
            StatusMessage =
                "A connection is already in progress.";
            return false;
        }

        return true;
    }

    private bool CanUseLAN()
    {
        // A transport already switched to Relay cannot
        // simply use SetConnectionData to switch back.
        if (transport.Protocol ==
            UnityTransport.ProtocolType.RelayUnityTransport)
        {
            StatusMessage =
                "Restart Play Mode to switch from Relay to LAN.";
            Debug.LogWarning(StatusMessage);
            return false;
        }

        return true;
    }

    private bool ValidateNetworkSetup()
    {
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("NetworkManager is missing.");
            return false;
        }

        if (transport == null)
            transport =
                networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("UnityTransport is missing.");
            return false;
        }

        return true;
    }

    public void Disconnect()
    {
        if (networkManager != null &&
            networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        RelayJoinCode = "";
        StatusMessage = "Disconnected.";
    }

    // ============================================
    // NETWORK CALLBACKS
    // ============================================

    private void OnEnable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback +=
                OnClientConnected;

            networkManager.OnClientDisconnectCallback +=
                OnClientDisconnected;
        }
    }

    private void Start()
    {
        if (networkManager == null) return;

        // Avoid duplicate subscriptions.
        networkManager.OnClientConnectedCallback -=
            OnClientConnected;
        networkManager.OnClientConnectedCallback +=
            OnClientConnected;

        networkManager.OnClientDisconnectCallback -=
            OnClientDisconnected;
        networkManager.OnClientDisconnectCallback +=
            OnClientDisconnected;
    }

    private void OnDisable()
    {
        if (networkManager == null) return;

        networkManager.OnClientConnectedCallback -=
            OnClientConnected;

        networkManager.OnClientDisconnectCallback -=
            OnClientDisconnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected! ID: {clientId}");

        if (networkManager != null &&
            !networkManager.IsHost)
        {
            StatusMessage = "Connected to host!";
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        Debug.LogWarning(
            $"Client disconnected! ID: {clientId}");

        if (networkManager != null &&
            !networkManager.IsHost)
        {
            StatusMessage = "Disconnected from host.";
        }
    }
}
