
using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class FurnaceNetworkBinding : MonoBehaviour
{
    [Header("Local Gameplay References")]
    [SerializeField] private FurnaceController furnace;
    [SerializeField] private FurnaceDisplay display;

    private FurnaceNetworkState networkState;
    private bool subscribedToFurnace;

    private void OnEnable()
    {
        StartCoroutine(RegisterWhenReady());
    }

    private IEnumerator RegisterWhenReady()
    {
        // Wait until the shared network state is available.
        while (FurnaceNetworkState.Instance == null ||
               !FurnaceNetworkState.Instance.IsSpawned)
        {
            yield return null;
        }

        networkState = FurnaceNetworkState.Instance;

        if (networkState.IsServer)
        {
            // Explorer: publish changes from the physical furnace.
            if (furnace == null)
            {
                Debug.LogError(
                    "Explorer FurnaceController is not assigned."
                );
                yield break;
            }

            furnace.StateChanged += HandleFurnaceChanged;
            subscribedToFurnace = true;

            // Publish the complete current state immediately.
            networkState.PublishState(furnace.State);

            Debug.Log("Explorer furnace registered.");
        }
        else
        {
            // Navigator: listen for synchronized updates.
            if (display == null)
            {
                Debug.LogError(
                    "Navigator FurnaceDisplay is not assigned."
                );
                yield break;
            }

            networkState.OnStateChanged += HandleNetworkChanged;

            // Show the current synchronized state immediately.
            // OnValueChanged only fires for subsequent changes.
            display.ShowState(networkState.CurrentState);

            Debug.Log("Navigator furnace display registered.");
        }
    }

    private void HandleFurnaceChanged(FurnaceState state)
    {
        if (networkState != null)
            networkState.PublishState(state);
    }

    private void HandleNetworkChanged(FurnaceState state)
    {
        if (display != null)
            display.ShowState(state);
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (subscribedToFurnace && furnace != null)
        {
            furnace.StateChanged -= HandleFurnaceChanged;
            subscribedToFurnace = false;
        }

        if (networkState != null)
        {
            networkState.OnStateChanged -= HandleNetworkChanged;
            networkState = null;
        }
    }
}
