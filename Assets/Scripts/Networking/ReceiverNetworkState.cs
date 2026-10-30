
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public class ReceiverNetworkState : NetworkBehaviour
{
    public static ReceiverNetworkState Instance { get; private set; }

    // Each machine has its own local receiver.
    private ReceiverController localReceiver;

    public override void OnNetworkSpawn()
    {
        Instance = this;

        Debug.Log(
            $"ReceiverNetworkState spawned. Server: {IsServer}"
        );
    }

    public override void OnNetworkDespawn()
    {
        localReceiver = null;

        if (Instance == this)
            Instance = null;
    }

    // Called by the receiver in the local gameplay scene.
    public void RegisterLocalReceiver(ReceiverController receiver)
    {
        localReceiver = receiver;

        Debug.Log(
            $"Local receiver registered: {receiver.name}"
        );
    }

    public void UnregisterLocalReceiver(ReceiverController receiver)
    {
        if (localReceiver == receiver)
            localReceiver = null;
    }

    // Public entry point for Navigator inputs.
    public void SendSymbol(string symbol)
    {
        if (!IsSpawned || !IsClient)
        {
            Debug.LogWarning("Receiver network is not ready.");
            return;
        }

        if (IsServer)
        {
            Debug.LogWarning(
                "Only the Navigator sends receiver symbols."
            );
            return;
        }

        if (!MessageProtocol.IsValidSymbol(symbol))
        {
            Debug.LogWarning($"Invalid symbol: {symbol}");
            return;
        }

        Debug.Log($"Navigator requested symbol: {symbol}");

        SendSymbolRpc(new FixedString32Bytes(symbol));
    }

    // The request travels from Navigator to Explorer host.
    [Rpc(SendTo.Server, InvokePermission = RpcInvokePermission.Everyone)]
    private void SendSymbolRpc(
        FixedString32Bytes symbol,
        RpcParams rpcParams = default)
    {
        ulong sender = rpcParams.Receive.SenderClientId;

        // Reject messages originating from the host.
        if (sender == NetworkManager.ServerClientId)
            return;

        string symbolName = symbol.ToString();

        if (!MessageProtocol.IsValidSymbol(symbolName))
        {
            Debug.LogWarning(
                $"Rejected invalid symbol: {symbolName}"
            );
            return;
        }

        Debug.Log(
            $"Explorer accepted symbol: {symbolName}"
        );

        // Relay the accepted symbol to both players.
        DeliverSymbolRpc(symbol);
    }

    // Runs on the Explorer host and Navigator client.
    [Rpc(SendTo.ClientsAndHost)]
    private void DeliverSymbolRpc(FixedString32Bytes symbol)
    {
        string symbolName = symbol.ToString();

        if (localReceiver == null)
        {
            Debug.LogWarning(
                "ReceiverNetworkState has no local receiver registered."
            );
            return;
        }

        // Reuse all the existing receiver functionality.
        localReceiver.AddSymbol(symbolName);

        Debug.Log(
            $"Receiver updated with: {symbolName}"
        );
    }

    // Temporary IMGUI test controls.
    private void OnGUI()
    {
        if (!IsSpawned || !IsClient || IsServer)
            return;

        GUILayout.BeginArea(
            new Rect(20, 300, 300, 210),
            GUI.skin.box
        );

        GUILayout.Label("RECEIVER NETWORK TEST");

        if (GUILayout.Button("Send LIGHT"))
            SendSymbol(MessageProtocol.Light);

        if (GUILayout.Button("Send DOOR"))
            SendSymbol(MessageProtocol.Door);

        if (GUILayout.Button("Send ENEMY"))
            SendSymbol(MessageProtocol.Enemy);

        if (GUILayout.Button("Send RETURN"))
            SendSymbol(MessageProtocol.Return);

        GUILayout.EndArea();
    }
}
