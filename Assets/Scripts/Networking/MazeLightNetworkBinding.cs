
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MazeLightNetworkBinding : MonoBehaviour
{
    [Header("Generated Maze or Map")]
    [SerializeField] private Transform generatedRoot;

    private MazeNetworkState mazeNetworkState;
    private Coroutine connectionCoroutine;

    private readonly Dictionary<int, ExplorerLight>
        explorerLights = new();

    private readonly Dictionary<int, NavigatorLightNode>
        navigatorLights = new();

    private void OnEnable()
    {
        CacheLights();
        connectionCoroutine = StartCoroutine(ConnectWhenReady());
    }

    private void OnDisable()
    {
        if (connectionCoroutine != null)
        {
            StopCoroutine(connectionCoroutine);
            connectionCoroutine = null;
        }

        if (mazeNetworkState != null)
        {
            mazeNetworkState.LightStateChanged -= ApplyLightState;
            mazeNetworkState = null;
        }
    }

    private void CacheLights()
    {
        explorerLights.Clear();
        navigatorLights.Clear();

        if (generatedRoot == null)
        {
            Debug.LogError(
                "Generated Root is not assigned.",
                this
            );
            return;
        }

        // Find Explorer 3D lights.
        foreach (ExplorerLight light in
            generatedRoot.GetComponentsInChildren<ExplorerLight>(true))
        {
            if (!explorerLights.TryAdd(light.LightId, light))
            {
                Debug.LogWarning(
                    $"Duplicate Explorer light ID: {light.LightId}",
                    this
                );
            }
        }

        // Find Navigator map lights.
        foreach (NavigatorLightNode light in
            generatedRoot.GetComponentsInChildren<NavigatorLightNode>(true))
        {
            if (!navigatorLights.TryAdd(light.LightId, light))
            {
                Debug.LogWarning(
                    $"Duplicate Navigator light ID: {light.LightId}",
                    this
                );
            }
        }

        Debug.Log(
            $"[Light Binding] Found {explorerLights.Count} Explorer lights, " +
            $"{navigatorLights.Count} Navigator lights.",
            this
        );
    }

    private IEnumerator ConnectWhenReady()
    {
        // GameSession and the role scenes load separately.
        while (mazeNetworkState == null)
        {
            MazeNetworkState found =
                FindAnyObjectByType<MazeNetworkState>();

            if (found != null && found.IsSpawned)
            {
                mazeNetworkState = found;
                break;
            }

            yield return new WaitForSeconds(0.2f);
        }

        // Subscribe to future changes.
        mazeNetworkState.LightStateChanged += ApplyLightState;

        // Apply the current state of every light immediately.
        mazeNetworkState.ApplyCurrentLightStates(ApplyLightState);

        connectionCoroutine = null;
    }

    private void ApplyLightState(int lightId, MazeLightState state)
    {
        bool found = false;

        // Explorer physical light.
        if (explorerLights.TryGetValue(
            lightId, out ExplorerLight explorerLight))
        {
            explorerLight.SetState(state);
            found = true;
        }

        // Navigator UI light.
        if (navigatorLights.TryGetValue(
            lightId, out NavigatorLightNode navigatorLight))
        {
            navigatorLight.SetState(state);
            found = true;
        }

        if (!found)
        {
            Debug.LogWarning(
                $"[Light Binding] Could not find light {lightId}.",
                this
            );
        }
    }

    // We will use this when the maze is regenerated.
    public void RefreshLightReferences()
    {
        CacheLights();

        if (mazeNetworkState != null && mazeNetworkState.IsSpawned)
        {
            mazeNetworkState.ApplyCurrentLightStates(
                ApplyLightState
            );
        }
    }
}
