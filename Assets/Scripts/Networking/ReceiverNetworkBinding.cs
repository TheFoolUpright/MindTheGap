
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(ReceiverController))]
public class ReceiverNetworkBinding : MonoBehaviour
{
    private ReceiverController receiver;
    private ReceiverNetworkState registeredNetwork;

    private void Awake()
    {
        receiver = GetComponent<ReceiverController>();
    }

    private void OnEnable()
    {
        StartCoroutine(RegisterWhenReady());
    }

    private IEnumerator RegisterWhenReady()
    {
        // Role-specific scenes may load after GameSession.
        while (ReceiverNetworkState.Instance == null ||
               !ReceiverNetworkState.Instance.IsSpawned)
        {
            yield return null;
        }

        registeredNetwork = ReceiverNetworkState.Instance;
        registeredNetwork.RegisterLocalReceiver(receiver);
    }

    private void OnDisable()
    {
        StopAllCoroutines();

        if (registeredNetwork != null)
        {
            registeredNetwork.UnregisterLocalReceiver(receiver);
            registeredNetwork = null;
        }
    }
}
