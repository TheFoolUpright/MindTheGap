
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameSessionLoader : MonoBehaviour
{
    [Header("Role Scenes")]
    [SerializeField] private string explorerScene = "ExplorerScene";
    [SerializeField] private string navigatorScene = "NavigatorScene";

    private bool sceneLoadingStarted;
    private string loadedRoleScene;

    private IEnumerator Start()
    {
        NetworkManager net = NetworkManager.Singleton;

        if (net == null)
        {
            Debug.LogError(
                "GameSessionLoader: NetworkManager not found."
            );
            yield break;
        }

        // Wait until Netcode considers this instance
        // connected before deciding which scene to load.
        while (!net.IsConnectedClient)
        {
            yield return null;
        }

        if (sceneLoadingStarted)
            yield break;

        sceneLoadingStarted = true;

        // In Mind the Gap, the host is always Explorer.
        string targetScene = net.IsHost
            ? explorerScene
            : navigatorScene;

        Debug.Log(
            $"Loading local role scene: {targetScene}"
        );

        // Load the role scene alongside GameSession.
        // This is a local load, not a Netcode scene event.
        AsyncOperation operation =
            SceneManager.LoadSceneAsync(
                targetScene,
                LoadSceneMode.Additive
            );

        if (operation == null)
        {
            Debug.LogError(
                $"Could not load scene: {targetScene}"
            );
            yield break;
        }

        yield return operation;

        loadedRoleScene = targetScene;

        Debug.Log(
            $"Role scene loaded: {loadedRoleScene}"
        );
    }

    private void OnGUI()
    {
        GUILayout.BeginArea(
            new Rect(20, 20, 350, 105),
            GUI.skin.box
        );

        GUILayout.Label("MIND THE GAP - GAME SESSION");

        GUILayout.Label(
            string.IsNullOrEmpty(loadedRoleScene)
                ? "Loading gameplay environment..."
                : $"Loaded: {loadedRoleScene}"
        );

        GUILayout.EndArea();
    }
}
