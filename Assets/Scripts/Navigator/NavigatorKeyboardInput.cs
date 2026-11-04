
using UnityEngine;
using UnityEngine.InputSystem;

public class NavigatorKeyboardInput : MonoBehaviour
{
    [Header("Code Entry")]
    [SerializeField] private NavigatorCodeEntry codeEntry;

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || codeEntry == null)
            return;

        // Rotary dial.
        if (keyboard.qKey.wasPressedThisFrame)
            codeEntry.RotateDial(-1);

        if (keyboard.eKey.wasPressedThisFrame)
            codeEntry.RotateDial(1);

        // Four binary switches.
        if (keyboard.zKey.wasPressedThisFrame)
            codeEntry.ToggleSwitch(0);

        if (keyboard.xKey.wasPressedThisFrame)
            codeEntry.ToggleSwitch(1);

        if (keyboard.cKey.wasPressedThisFrame)
            codeEntry.ToggleSwitch(2);

        if (keyboard.vKey.wasPressedThisFrame)
            codeEntry.ToggleSwitch(3);

        // Six directional inputs, entered one press at a time.
        if (keyboard.upArrowKey.wasPressedThisFrame)
            codeEntry.PressDirection(ControlDirection.Up);

        if (keyboard.rightArrowKey.wasPressedThisFrame)
            codeEntry.PressDirection(ControlDirection.Right);

        if (keyboard.downArrowKey.wasPressedThisFrame)
            codeEntry.PressDirection(ControlDirection.Down);

        if (keyboard.leftArrowKey.wasPressedThisFrame)
            codeEntry.PressDirection(ControlDirection.Left);

        // Correct an input.
        if (keyboard.backspaceKey.wasPressedThisFrame)
            codeEntry.UndoDirection();

        // Confirm completed combination.
        if (keyboard.enterKey.wasPressedThisFrame)
            codeEntry.ConfirmCode();
    }
}
