
using UnityEngine;
using UnityEngine.InputSystem;

// Number keys 1-9 and 0 send receiver symbols to the Explorer.
// Replaces the old IMGUI test buttons in ReceiverNetworkState.
public class NavigatorReceiverKeyboardInput : MonoBehaviour
{
    // Index 0 = key 1 ... index 8 = key 9, index 9 = key 0. Null = unassigned.
    private static readonly string[] KeySymbols =
    {
        ReceiverSymbols.Light,      // 1
        ReceiverSymbols.Door,       // 2
        ReceiverSymbols.Enemy,      // 3
        ReceiverSymbols.Return,     // 4
        ReceiverSymbols.Cancel,     // 5
        ReceiverSymbols.Approve,    // 6
        ReceiverSymbols.Cube,       // 7
        ReceiverSymbols.Pyramid,    // 8
        ReceiverSymbols.Disapprove, // 9
        null                        // 0
    };

    private void Update()
    {
        ReceiverNetworkState receiverNetwork = ReceiverNetworkState.Instance;

        // Same rule as the old buttons: Navigator client only.
        if (receiverNetwork == null ||
            !receiverNetwork.IsSpawned ||
            !receiverNetwork.IsClient ||
            receiverNetwork.IsServer)
            return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
            return;

        for (int i = 0; i < KeySymbols.Length; i++)
        {
            Key key = i == 9 ? Key.Digit0 : Key.Digit1 + i;

            if (KeySymbols[i] != null && keyboard[key].wasPressedThisFrame)
                receiverNetwork.SendSymbol(KeySymbols[i]);
        }
    }
}
