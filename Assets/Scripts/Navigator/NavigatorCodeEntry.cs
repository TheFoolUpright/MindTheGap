
using System;
using System.Collections.Generic;
using UnityEngine;

public class NavigatorCodeEntry : MonoBehaviour
{
    [Header("Code Manager")]
    [SerializeField] private NavigatorControlCodeManager codeManager;

    private int dial;
    private int toggleMask;

    private readonly List<ControlDirection> directions = new();

    public int Dial => dial;
    public int ToggleMask => toggleMask;
    public IReadOnlyList<ControlDirection> Directions => directions;

    public string Feedback { get; private set; } = "Enter a code";

    // The future prototype UI can listen for input changes.
    public event Action InputChanged;

    // Later, networking can listen for successful actions.
    public event Action<ControlCodeMatch> ValidCodeEntered;

    private void Awake()
    {
        if (codeManager != null)
            dial = codeManager.DialMinimum;
    }

    private void OnEnable()
    {
        if (codeManager != null)
            codeManager.CodesRegenerated += OnCodesRegenerated;
    }

    private void OnDisable()
    {
        if (codeManager != null)
            codeManager.CodesRegenerated -= OnCodesRegenerated;
    }

    // Simulates turning the physical dial.
    public void RotateDial(int steps)
    {
        if (codeManager == null)
            return;

        int min = codeManager.DialMinimum;
        int max = codeManager.DialMaximum;

        if (max < min)
            return;

        // Wrap from 9 to 0, or from 0 to 9.
        if (steps > 0)
            dial = dial >= max ? min : dial + 1;
        else if (steps < 0)
            dial = dial <= min ? max : dial - 1;

        Notify($"Dial: {dial}");
    }

    // Useful later when hardware reports an absolute dial position.
    public void SetDial(int value)
    {
        if (codeManager == null)
            return;

        dial = Mathf.Clamp(
            value,
            codeManager.DialMinimum,
            codeManager.DialMaximum
        );

        Notify($"Dial: {dial}");
    }

    // Switch indices are 0, 1, 2, and 3.
    public void ToggleSwitch(int index)
    {
        if (index < 0 || index >= 4)
            return;

        bool isOn = (toggleMask & (1 << index)) != 0;

        SetSwitchState(index, !isOn);
    }

    public void SetSwitchState(int index, bool isOn)
    {
        if (index < 0 || index >= 4)
            return;

        if (isOn)
            toggleMask |= 1 << index;
        else
            toggleMask &= ~(1 << index);

        string binary = Convert.ToString(toggleMask, 2).PadLeft(4, '0');

        Notify($"Switches: {binary}");
    }

    public void PressDirection(ControlDirection direction)
    {
        if (directions.Count >= ControlCode.DirectionCount)
            return;

        directions.Add(direction);

        Notify(
            $"Directions: {directions.Count}/{ControlCode.DirectionCount} " +
            $"[{string.Join(" → ", directions)}]"
        );
    }

    public void UndoDirection()
    {
        if (directions.Count == 0)
            return;

        directions.RemoveAt(directions.Count - 1);

        Notify($"Removed last direction. {directions.Count}/6 entered.");
    }

    public void ConfirmCode()
    {
        if (codeManager == null)
        {
            Notify("Code Manager not assigned.");
            return;
        }

        if (directions.Count != ControlCode.DirectionCount)
        {
            Notify("Enter all six directions before confirming.");
            return;
        }

        ControlCode enteredCode = new ControlCode(
            dial,
            toggleMask,
            directions
        );

        if (codeManager.TryResolveCode(
            enteredCode,
            out ControlCodeMatch match))
        {
            string target = match.Action == ControlCodeAction.ToggleDoor
                ? $"Door {match.DoorId}"
                : $"Light {match.LightId}";

            Notify($"Correct code! {match.Action}: {target}");

            ValidCodeEntered?.Invoke(match);

            // Clear only the directional sequence.
            directions.Clear();
            InputChanged?.Invoke();
        }
        else
        {
            // Keep the inputs so the player can correct a mistake.
            Notify("Invalid code. Use Backspace to correct it.");
        }
    }

    private void OnCodesRegenerated()
    {
        // A refresh invalidates any partially entered sequence.
        directions.Clear();

        // Dial and toggles stay where the physical controls are.
        Notify("New codes assigned. Direction sequence cleared.");
    }

    private void Notify(string message)
    {
        Feedback = message;
        Debug.Log($"[Navigator Code Entry] {message}", this);
        InputChanged?.Invoke();
    }
}
