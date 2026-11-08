
using System.Collections;
using System.Text;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.UI;
using UnityEngine.XR.Management;

// Lobby menu on a world-space canvas, so both the VR Explorer and the
// flat-screen Navigator can see and click it.
// - VR (headset active): spawns the XR rig, swaps the EventSystem to
//   XRUIInputModule and places the menu in front of the player's head.
// - Flat screen: keeps the scene camera and frames it on the menu.
public class LobbyHUD : MonoBehaviour
{
    private enum ViewMode
    {
        Auto,
        ForceVR,
        ForceFlat
    }

    [SerializeField] private LobbyManager lobbyManager;

    [Header("View")]
    [Tooltip("Auto picks VR when an XR headset is active.")]
    [SerializeField] private ViewMode viewMode = ViewMode.Auto;

    [Tooltip("XR rig spawned in the lobby when playing in VR.")]
    [SerializeField] private GameObject xrRigPrefab;

    [Tooltip("Camera used by the flat-screen player. Disabled in VR.")]
    [SerializeField] private Camera flatCamera;

    [Header("Menu Placement")]
    [Tooltip("World position of the menu for the flat-screen player.")]
    [SerializeField] private Vector3 flatMenuPosition = new Vector3(0f, 1.4f, 2f);

    [Tooltip("How far in front of the VR player's head the menu appears.")]
    [SerializeField] private float vrMenuDistance = 1.6f;

    [Tooltip("How far below eye level the menu centre sits in VR.")]
    [SerializeField] private float vrMenuDrop = 0.15f;

    // Canvas size in UI pixels; 1000 px = 1 metre.
    private const float CanvasWidth = 800f;
    private const float CanvasHeight = 1000f;
    private const float CanvasScale = 0.001f;

#if UNITY_EDITOR
    private const string DefaultRigPath =
        "Assets/Other/VRTemplateAssets/Prefabs/Setup/Complete XR Origin Set Up Hands Variant.prefab";
#endif

    // 0 = Relay, 1 = Local Network.
    private int connectionMode = 0;
    private string serverAddress = "127.0.0.1";
    private string relayJoinCode = "";

    private bool isVR;
    private Canvas canvas;

    // Widgets.
    private TextMeshProUGUI joinCodeLabel;
    private TextMeshProUGUI statusLabel;

    private GameObject connectGroup;
    private Button relayModeButton;
    private Button lanModeButton;
    private TextMeshProUGUI connectSectionLabel;
    private Button hostButton;
    private TextMeshProUGUI fieldLabel;
    private TMP_InputField addressField;
    private Button joinButton;
    private GameObject pleaseWaitLabel;

    private GameObject connectingGroup;

    private GameObject lobbyGroup;
    private TextMeshProUGUI roleLabel;
    private TextMeshProUGUI playersLabel;
    private Button readyButton;
    private TextMeshProUGUI readyButtonLabel;
    private Button startButton;
    private GameObject waitingLabel;

    private TextMeshProUGUI errorLabel;

#if UNITY_EDITOR
    private void OnValidate()
    {
        // Default to the same XR rig the Explorer scene uses.
        if (xrRigPrefab == null)
        {
            xrRigPrefab =
                UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(DefaultRigPath);
        }
    }
#endif

    private void Start()
    {
        if (flatCamera == null)
            flatCamera = Camera.main;

        isVR = viewMode == ViewMode.ForceVR ||
               (viewMode == ViewMode.Auto && IsXRActive());

        BuildMenu();

        if (isVR && xrRigPrefab != null)
        {
            SetUpVR();
        }
        else
        {
            if (isVR)
                Debug.LogWarning("LobbyHUD: VR mode but no XR rig prefab assigned. Using flat view.");

            isVR = false;
            SetUpFlat();
        }

        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    // ============================================
    // VIEW SETUP
    // ============================================

    private static bool IsXRActive()
    {
        XRGeneralSettings settings = XRGeneralSettings.Instance;

        return settings != null &&
               settings.Manager != null &&
               settings.Manager.activeLoader != null &&
               XRSettings.isDeviceActive;
    }

    private void SetUpFlat()
    {
        canvas.transform.SetPositionAndRotation(flatMenuPosition, Quaternion.identity);

        if (flatCamera == null)
        {
            Debug.LogWarning("LobbyHUD: No flat camera found.");
            return;
        }

        // Back the camera off until the whole menu fits on screen.
        float halfHeight = CanvasHeight * CanvasScale * 0.5f * 1.1f;
        float halfWidth = CanvasWidth * CanvasScale * 0.5f * 1.1f;
        float halfFov = flatCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float halfHorizontalFov = Mathf.Atan(Mathf.Tan(halfFov) * flatCamera.aspect);

        float distance = Mathf.Max(
            halfHeight / Mathf.Tan(halfFov),
            halfWidth / Mathf.Tan(halfHorizontalFov));

        flatCamera.transform.SetPositionAndRotation(
            flatMenuPosition - Vector3.forward * distance,
            Quaternion.identity);

        canvas.worldCamera = flatCamera;
    }

    private void SetUpVR()
    {
        // The flat camera would fight the rig's camera for the headset.
        if (flatCamera != null)
            flatCamera.gameObject.SetActive(false);

        GameObject rig = Instantiate(xrRigPrefab, Vector3.zero, Quaternion.identity);
        rig.name = xrRigPrefab.name;

        // XR rays need XRUIInputModule instead of the plain Input System module.
        EventSystem eventSystem = EventSystem.current != null
            ? EventSystem.current
            : FindFirstObjectByType<EventSystem>();

        if (eventSystem == null)
            eventSystem = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();

        var oldModule = eventSystem.GetComponent<InputSystemUIInputModule>();
        if (oldModule != null)
            DestroyImmediate(oldModule);

        if (eventSystem.GetComponent<XRUIInputModule>() == null)
            eventSystem.gameObject.AddComponent<XRUIInputModule>();

        canvas.gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();

        Camera headCamera = rig.GetComponentInChildren<Camera>();
        canvas.worldCamera = headCamera;

        // Start in front of the rig origin, then re-centre once tracking is live.
        PlaceInFront(rig.transform.position + Vector3.up * 1.6f, Vector3.forward);
        StartCoroutine(CentreOnHeadWhenTracked(headCamera));
    }

    private IEnumerator CentreOnHeadWhenTracked(Camera headCamera)
    {
        if (headCamera == null)
            yield break;

        // Give the headset a moment to report a real pose.
        yield return new WaitForSeconds(0.5f);

        Transform head = headCamera.transform;
        Vector3 forward = Vector3.ProjectOnPlane(head.forward, Vector3.up);

        if (forward.sqrMagnitude < 0.01f)
            forward = Vector3.forward;

        PlaceInFront(head.position, forward.normalized);
    }

    private void PlaceInFront(Vector3 headPosition, Vector3 flatForward)
    {
        Vector3 position = headPosition + flatForward * vrMenuDistance;
        position.y = headPosition.y - vrMenuDrop;

        canvas.transform.SetPositionAndRotation(
            position,
            Quaternion.LookRotation(flatForward, Vector3.up));
    }

    // ============================================
    // MENU CONSTRUCTION
    // ============================================

    private void BuildMenu()
    {
        var canvasObject = new GameObject("LobbyWorldCanvas", typeof(RectTransform));
        canvasObject.layer = 5; // UI

        canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvasObject.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        canvasObject.AddComponent<GraphicRaycaster>();

        var canvasRect = (RectTransform)canvasObject.transform;
        canvasRect.sizeDelta = new Vector2(CanvasWidth, CanvasHeight);
        canvasRect.localScale = Vector3.one * CanvasScale;

        // Background panel with the vertical layout.
        RectTransform panel = LobbyUIBuilder.CreateRect("Panel", canvasRect);
        LobbyUIBuilder.Stretch(panel);
        panel.gameObject.AddComponent<Image>().color = LobbyUIBuilder.PanelColor;

        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(40, 40, 40, 40);
        layout.spacing = 16f;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        LobbyUIBuilder.Label(panel, "MIND THE GAP", 56f, FontStyles.Bold, TextAlignmentOptions.Center);

        joinCodeLabel = LobbyUIBuilder.Label(panel, "", 40f, FontStyles.Bold, TextAlignmentOptions.Center);
        joinCodeLabel.color = new Color(1f, 0.85f, 0.3f);

        errorLabel = LobbyUIBuilder.Label(panel, "", 30f);
        errorLabel.color = new Color(1f, 0.5f, 0.5f);

        BuildConnectGroup(panel);
        BuildConnectingGroup(panel);
        BuildLobbyGroup(panel);

        statusLabel = LobbyUIBuilder.Label(panel, "", 26f, FontStyles.Italic);
        statusLabel.color = new Color(0.8f, 0.8f, 0.8f);
    }

    private void BuildConnectGroup(Transform parent)
    {
        RectTransform group = LobbyUIBuilder.Group(parent, "Connect");
        connectGroup = group.gameObject;

        LobbyUIBuilder.Label(group, "Connection Method:", 30f);

        RectTransform modeRow = LobbyUIBuilder.Row(group, "Mode", 70f);
        relayModeButton = LobbyUIBuilder.Button(modeRow, "Relay", () => SetConnectionMode(0));
        lanModeButton = LobbyUIBuilder.Button(modeRow, "LAN", () => SetConnectionMode(1));

        connectSectionLabel = LobbyUIBuilder.Label(group, "", 30f, FontStyles.Bold);

        hostButton = LobbyUIBuilder.Button(group, "Host as Explorer", OnHostClicked);

        fieldLabel = LobbyUIBuilder.Label(group, "", 30f);

        addressField = LobbyUIBuilder.InputField(group, "");
        addressField.onValueChanged.AddListener(OnAddressChanged);

        joinButton = LobbyUIBuilder.Button(group, "Join as Navigator", OnJoinClicked);

        pleaseWaitLabel = LobbyUIBuilder.Label(group, "Please wait...", 30f).gameObject;

        ApplyConnectionMode();
    }

    private void BuildConnectingGroup(Transform parent)
    {
        RectTransform group = LobbyUIBuilder.Group(parent, "Connecting");
        connectingGroup = group.gameObject;

        LobbyUIBuilder.Label(group, "Connecting to lobby...", 32f);
        LobbyUIBuilder.Button(group, "Cancel / Disconnect", () => lobbyManager.Disconnect());
    }

    private void BuildLobbyGroup(Transform parent)
    {
        RectTransform group = LobbyUIBuilder.Group(parent, "Lobby");
        lobbyGroup = group.gameObject;

        roleLabel = LobbyUIBuilder.Label(group, "", 34f, FontStyles.Bold);
        LobbyUIBuilder.Label(group, "PLAYERS", 30f, FontStyles.Bold);
        playersLabel = LobbyUIBuilder.Label(group, "", 30f);

        readyButton = LobbyUIBuilder.Button(group, "Ready", OnReadyClicked, out readyButtonLabel);
        startButton = LobbyUIBuilder.Button(group, "Start Game", OnStartClicked);
        waitingLabel = LobbyUIBuilder.Label(group, "Waiting for both players to be ready...", 28f).gameObject;

        LobbyUIBuilder.Button(group, "Disconnect", () => lobbyManager.Disconnect());
    }

    // ============================================
    // STATE REFRESH
    // ============================================

    private void Refresh()
    {
        if (canvas == null)
            return;

        if (lobbyManager == null)
        {
            ShowOnly(null);
            errorLabel.text = "LobbyManager not assigned.";
            return;
        }

        NetworkManager net = NetworkManager.Singleton;

        if (net == null)
        {
            ShowOnly(null);
            errorLabel.text = "NetworkManager not found.";
            return;
        }

        errorLabel.text = "";

        // Host's Relay join code, so the Explorer can read it out.
        bool showCode = net.IsHost && !string.IsNullOrEmpty(lobbyManager.RelayJoinCode);
        joinCodeLabel.gameObject.SetActive(showCode);
        if (showCode)
            joinCodeLabel.text = "RELAY JOIN CODE: " + lobbyManager.RelayJoinCode;

        if (!net.IsListening)
        {
            ShowOnly(connectGroup);
            RefreshConnect();
        }
        else if (!net.IsConnectedClient ||
                 Lobby.Instance == null ||
                 !Lobby.Instance.IsSpawned)
        {
            ShowOnly(connectingGroup);
        }
        else
        {
            ShowOnly(lobbyGroup);
            RefreshLobby(net, Lobby.Instance);
        }

        bool hasStatus = !string.IsNullOrEmpty(lobbyManager.StatusMessage);
        statusLabel.gameObject.SetActive(hasStatus);
        if (hasStatus)
            statusLabel.text = "Status: " + lobbyManager.StatusMessage;
    }

    private void ShowOnly(GameObject group)
    {
        SetActive(connectGroup, group == connectGroup);
        SetActive(connectingGroup, group == connectingGroup);
        SetActive(lobbyGroup, group == lobbyGroup);
        errorLabel.gameObject.SetActive(group == null);
    }

    private static void SetActive(GameObject go, bool active)
    {
        if (go != null && go.activeSelf != active)
            go.SetActive(active);
    }

    private void RefreshConnect()
    {
        bool canClick = !lobbyManager.IsConnecting;

        relayModeButton.interactable = canClick;
        lanModeButton.interactable = canClick;
        hostButton.interactable = canClick;
        addressField.interactable = canClick;
        joinButton.interactable = canClick;

        SetActive(pleaseWaitLabel, lobbyManager.IsConnecting);
    }

    private void RefreshLobby(NetworkManager net, Lobby lobby)
    {
        roleLabel.text = net.IsHost
            ? "Role: Explorer (Host)"
            : "Role: Navigator (Client)";

        var players = new StringBuilder();
        foreach (LobbyEntry player in lobby.Players)
        {
            players.AppendLine($"{player.Role} - {(player.Ready ? "[READY]" : "[NOT READY]")}");
        }
        playersLabel.text = players.ToString().TrimEnd();

        if (lobby.TryGetLocalPlayer(out LobbyEntry me))
        {
            readyButton.interactable = true;
            readyButtonLabel.text = me.Ready ? "Cancel Ready" : "Ready";
        }
        else
        {
            readyButton.interactable = false;
            readyButtonLabel.text = "Registering player...";
        }

        bool isHost = net.IsHost;
        bool canStart = isHost && lobby.CanStartGame();

        SetActive(startButton.gameObject, isHost);
        startButton.interactable = canStart;
        SetActive(waitingLabel, isHost && !canStart);
    }

    // ============================================
    // BUTTON HANDLERS
    // ============================================

    private void SetConnectionMode(int mode)
    {
        connectionMode = mode;
        ApplyConnectionMode();
    }

    private void ApplyConnectionMode()
    {
        bool relay = connectionMode == 0;

        relayModeButton.image.color = relay ? LobbyUIBuilder.SelectedColor : LobbyUIBuilder.ButtonColor;
        lanModeButton.image.color = relay ? LobbyUIBuilder.ButtonColor : LobbyUIBuilder.SelectedColor;

        connectSectionLabel.text = relay ? "Unity Relay" : "Local Network";
        fieldLabel.text = relay ? "Relay Join Code:" : "Host IP Address:";

        // Each mode keeps its own field value.
        addressField.SetTextWithoutNotify(relay ? relayJoinCode : serverAddress);
        ((TextMeshProUGUI)addressField.placeholder).text =
            relay ? "Enter join code" : "127.0.0.1";
    }

    private void OnAddressChanged(string value)
    {
        if (connectionMode == 0)
            relayJoinCode = value;
        else
            serverAddress = value;
    }

    private void OnHostClicked()
    {
        if (connectionMode == 0)
            lobbyManager.StartExplorerHostRelay();
        else
            lobbyManager.StartExplorerHost();
    }

    private void OnJoinClicked()
    {
        if (connectionMode == 0)
        {
            lobbyManager.JoinAsNavigatorRelay(relayJoinCode);
        }
        else
        {
            lobbyManager.SetServerAddress(serverAddress);
            lobbyManager.JoinAsNavigator();
        }
    }

    private void OnReadyClicked()
    {
        if (Lobby.Instance != null)
            Lobby.Instance.ToggleReady();
    }

    private void OnStartClicked()
    {
        if (Lobby.Instance != null && Lobby.Instance.CanStartGame())
            Lobby.Instance.StartGame();
    }
}
