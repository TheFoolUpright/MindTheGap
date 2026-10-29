
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;

public class LobbyManager : MonoBehaviour
{
    [SerializeField] private string serverAddress = "127.0.0.1";
    [SerializeField] private ushort port = 7777;


    [SerializeField] private NetworkManager networkManager;

    private UnityTransport transport;


    private void Awake()
    {
        // Use the Inspector reference if available.
        // Otherwise, look for the active NetworkManager.
        if (networkManager == null)
        {
            networkManager = NetworkManager.Singleton;
        }

        if (networkManager == null)
        {
            networkManager = FindFirstObjectByType<NetworkManager>();
        }

        if (networkManager == null)
        {
            Debug.LogError(
                "LobbyManager: No NetworkManager found in this scene!"
            );
            return;
        }

        transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError(
                "LobbyManager: UnityTransport component not found!"
            );
        }
    }

    public void SetServerAddress(string address)
    {
        serverAddress = address;
    }

    public void StartExplorerHost()
    {
        if (!ValidateNetworkSetup()) return;

        transport.SetConnectionData(
            "127.0.0.1",
            port,
            "0.0.0.0"
        );

        if (networkManager.StartHost())
            Debug.Log("Explorer host started.");
        else
            Debug.LogError("Failed to start Explorer host.");
    }

    public void JoinAsNavigator()
    {
        if (!ValidateNetworkSetup()) return;

        transport.SetConnectionData(serverAddress, port);

        if (networkManager.StartClient())
            Debug.Log("Navigator connection started.");
        else
            Debug.LogError("Failed to start Navigator client.");
    }

    public void Disconnect()
    {
        if (networkManager != null && networkManager.IsListening)
            networkManager.Shutdown();
    }

    private bool ValidateNetworkSetup()
    {
        // Try again in case NetworkManager was initialized after Awake.
        if (networkManager == null)
            networkManager = NetworkManager.Singleton;

        if (networkManager == null)
        {
            Debug.LogError("Cannot connect: NetworkManager is missing.");
            return false;
        }

        if (transport == null)
            transport = networkManager.GetComponent<UnityTransport>();

        if (transport == null)
        {
            Debug.LogError("Cannot connect: UnityTransport is missing.");
            return false;
        }

        return true;
    }


    private void OnEnable()
    {
        if (networkManager != null)
            networkManager.OnClientConnectedCallback += OnClientConnected;
    }

    private void Start()
    {
        // Subscribe here if Awake/OnEnable ran before
        // the reference was available.
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnDisable()
    {
        if (networkManager != null)
            networkManager.OnClientConnectedCallback -= OnClientConnected;
    }

    private void OnClientConnected(ulong clientId)
    {
        Debug.Log($"Client connected! ID: {clientId}");
    }

}
