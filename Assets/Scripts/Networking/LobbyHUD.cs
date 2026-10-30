
using Unity.Netcode;
using UnityEngine;

public class LobbyHUD : MonoBehaviour
{
    [SerializeField] private LobbyManager lobbyManager;

    private string serverAddress = "127.0.0.1";


    private void OnGUI()
    {
        float menuWidth = Mathf.Min(360f, Screen.width - 40f);
        float menuHeight = Mathf.Min(400f, Screen.height - 40f);

        // Prevent negative sizes in very small windows.
        menuWidth = Mathf.Max(100f, menuWidth);
        menuHeight = Mathf.Max(100f, menuHeight);

        // Center the menu in the current game window.
        float x = (Screen.width - menuWidth) / 2f;
        float y = (Screen.height - menuHeight) / 2f;

        GUILayout.BeginArea(
            new Rect(x, y, menuWidth, menuHeight),
            GUI.skin.box
        );

        GUILayout.Label("MIND THE GAP");
        GUILayout.Space(10);

        NetworkManager net = NetworkManager.Singleton;

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
        }
        else
        {
            DrawLobby(net, Lobby.Instance);
        }

        GUILayout.EndArea();
    }


    private void DrawConnectionMenu()
    {
        GUILayout.Label("Multiplayer Lobby");

        if (GUILayout.Button("Host as Explorer"))
        {
            lobbyManager.StartExplorerHost();
        }

        GUILayout.Space(10);

        GUILayout.Label("Host IP Address:");
        serverAddress = GUILayout.TextField(serverAddress);

        if (GUILayout.Button("Join as Navigator"))
        {
            lobbyManager.SetServerAddress(serverAddress);
            lobbyManager.JoinAsNavigator();
        }
    }

    private void DrawLobby(NetworkManager net, Lobby lobby)
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
                player.Ready ? "[READY]" : "[NOT READY]";

            GUILayout.Label(
                $"{player.Role} - {readyStatus}"
            );
        }

        GUILayout.Space(15);

        if (lobby.TryGetLocalPlayer(out LobbyEntry me))
        {
            string buttonLabel = me.Ready
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
