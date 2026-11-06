
using Unity.Netcode;
using UnityEngine;

public class LobbyHUD : MonoBehaviour
{
    [SerializeField] private LobbyManager lobbyManager;

    // 0 = Relay, 1 = Local Network.
    private int connectionMode = 0;

    private static readonly string[] ConnectionModes =
    {
        "Relay",
        "LAN"
    };

    private string serverAddress = "127.0.0.1";
    private string relayJoinCode = "";

    private void OnGUI()
    {
        float menuWidth =
            Mathf.Min(400f, Screen.width - 40f);

        float menuHeight =
            Mathf.Min(460f, Screen.height - 40f);

        menuWidth = Mathf.Max(100f, menuWidth);
        menuHeight = Mathf.Max(100f, menuHeight);

        float x = (Screen.width - menuWidth) / 2f;
        float y = (Screen.height - menuHeight) / 2f;

        GUILayout.BeginArea(
            new Rect(x, y, menuWidth, menuHeight),
            GUI.skin.box
        );

        GUILayout.Label("MIND THE GAP");
        GUILayout.Space(10);

        if (lobbyManager == null)
        {
            GUILayout.Label("LobbyManager not assigned.");
            GUILayout.EndArea();
            return;
        }

        NetworkManager net = NetworkManager.Singleton;

        // Display the host's Relay join code.
        if (net != null &&
            net.IsHost &&
            !string.IsNullOrEmpty(
                lobbyManager.RelayJoinCode))
        {
            GUILayout.Label(
                "RELAY JOIN CODE: " +
                lobbyManager.RelayJoinCode);

            GUILayout.Space(10);
        }

        if (net == null)
        {
            GUILayout.Label("NetworkManager not found.");
        }
        else if (!net.IsListening)
        {
            DrawConnectionMenu();
        }
        else if (!net.IsConnectedClient ||
                 Lobby.Instance == null ||
                 !Lobby.Instance.IsSpawned)
        {
            GUILayout.Label("Connecting to lobby...");

            if (GUILayout.Button("Cancel / Disconnect"))
            {
                lobbyManager.Disconnect();
            }
        }
        else
        {
            DrawLobby(net, Lobby.Instance);
        }

        // Show connection progress and error messages.
        if (!string.IsNullOrEmpty(
            lobbyManager.StatusMessage))
        {
            GUILayout.Space(10);
            GUILayout.Label(
                "Status: " + lobbyManager.StatusMessage);
        }

        GUILayout.EndArea();
    }

    // ============================================
    // CONNECTION MENU
    // ============================================

    private void DrawConnectionMenu()
    {
        GUILayout.Label("Multiplayer Lobby");
        GUILayout.Space(10);

        bool wasEnabled = GUI.enabled;
        GUI.enabled =
            wasEnabled && !lobbyManager.IsConnecting;

        // Choose Relay or LAN before connecting.
        GUILayout.Label("Connection Method:");

        connectionMode = GUILayout.Toolbar(
            connectionMode,
            ConnectionModes
        );

        GUILayout.Space(15);

        if (connectionMode == 0)
        {
            DrawRelayMenu();
        }
        else
        {
            DrawLANMenu();
        }

        GUI.enabled = wasEnabled;

        if (lobbyManager.IsConnecting)
        {
            GUILayout.Space(10);
            GUILayout.Label("Please wait...");
        }
    }

    // ============================================
    // RELAY MENU
    // ============================================

    private void DrawRelayMenu()
    {
        GUILayout.Label("Unity Relay");

        if (GUILayout.Button("Host as Explorer"))
        {
            lobbyManager.StartExplorerHostRelay();
        }

        GUILayout.Space(10);

        GUILayout.Label("Relay Join Code:");

        relayJoinCode =
            GUILayout.TextField(relayJoinCode);

        if (GUILayout.Button("Join as Navigator"))
        {
            lobbyManager.JoinAsNavigatorRelay(
                relayJoinCode
            );
        }
    }

    // ============================================
    // LOCAL NETWORK MENU
    // ============================================

    private void DrawLANMenu()
    {
        GUILayout.Label("Local Network");

        if (GUILayout.Button("Host as Explorer"))
        {
            lobbyManager.StartExplorerHost();
        }

        GUILayout.Space(10);

        GUILayout.Label("Host IP Address:");

        serverAddress =
            GUILayout.TextField(serverAddress);

        if (GUILayout.Button("Join as Navigator"))
        {
            lobbyManager.SetServerAddress(
                serverAddress
            );

            lobbyManager.JoinAsNavigator();
        }
    }

    // ============================================
    // EXISTING LOBBY
    // ============================================

    private void DrawLobby(
        NetworkManager net,
        Lobby lobby)
    {
        GUILayout.Label(
            net.IsHost
                ? "Role: Explorer (Host)"
                : "Role: Navigator (Client)"
        );

        GUILayout.Space(10);

        GUILayout.Label("PLAYERS");

        foreach (LobbyEntry player in lobby.Players)
        {
            string readyStatus =
                player.Ready
                    ? "[READY]"
                    : "[NOT READY]";

            GUILayout.Label(
                $"{player.Role} - {readyStatus}"
            );
        }

        GUILayout.Space(15);

        if (lobby.TryGetLocalPlayer(
            out LobbyEntry me))
        {
            string buttonLabel =
                me.Ready
                    ? "Cancel Ready"
                    : "Ready";

            if (GUILayout.Button(buttonLabel))
            {
                lobby.ToggleReady();
            }
        }
        else
        {
            GUILayout.Label("Registering player...");
        }

        GUILayout.Space(10);

        if (net.IsHost)
        {
            bool canStart = lobby.CanStartGame();

            GUI.enabled = canStart;

            if (GUILayout.Button("Start Game"))
            {
                lobby.StartGame();
            }

            GUI.enabled = true;

            if (!canStart)
            {
                GUILayout.Label(
                    "Waiting for both players to be ready..."
                );
            }
        }

        GUILayout.Space(15);

        if (GUILayout.Button("Disconnect"))
        {
            lobbyManager.Disconnect();
        }
    }
}
