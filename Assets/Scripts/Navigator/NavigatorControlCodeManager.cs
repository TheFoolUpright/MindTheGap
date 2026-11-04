
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ControlCodeAction
{
    ToggleDoor,
    ToggleLight,
    OverloadLight
}

// Describes the action associated with an entered code.
public struct ControlCodeMatch
{
    public ControlCodeAction Action { get; }
    public string DoorId { get; }
    public int LightId { get; }

    public ControlCodeMatch(
        ControlCodeAction action,
        string doorId,
        int lightId)
    {
        Action = action;
        DoorId = doorId;
        LightId = lightId;
    }
}

public class NavigatorControlCodeManager : MonoBehaviour
{
    [Header("Maze Layout")]
    [SerializeField] private MazeLayoutData layout;

    [Header("Dial Range")]
    [SerializeField, Min(0)] private int dialMinimum = 0;
    [SerializeField, Min(0)] private int dialMaximum = 9;

    public int DialMinimum => dialMinimum;
    public int DialMaximum => dialMaximum;

    private readonly Dictionary<string, ControlCode> doorCodes = new();

    private readonly Dictionary<int, LightCodePair> lightCodes = new();

    private readonly Dictionary<ControlCode, ControlCodeMatch> codeActions = new();

    private readonly System.Random random = new();

    public event Action CodesRegenerated;

    private struct LightCodePair
    {
        public ControlCode Toggle;
        public ControlCode Overload;

        public LightCodePair(ControlCode toggle, ControlCode overload)
        {
            Toggle = toggle;
            Overload = overload;
        }
    }

    private void Start()
    {
        // Prototype: generate codes when the Navigator scene starts.
        RegenerateCodes();
    }

    // Later, the maze refresh system will call this method.
    public void SetLayoutAndRegenerate(MazeLayoutData newLayout)
    {
        layout = newLayout;
        RegenerateCodes();
    }

    [ContextMenu("Regenerate Control Codes")]
    public void RegenerateCodes()
    {
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Generate codes during Play Mode.", this);
            return;
        }

        doorCodes.Clear();
        lightCodes.Clear();
        codeActions.Clear();

        if (layout == null || !layout.IsConfigured)
        {
            Debug.LogError("Assign a valid MazeLayoutData.", this);
            return;
        }

        if (dialMinimum < 0 ||
            dialMaximum < dialMinimum ||
            dialMaximum == int.MaxValue)
        {
            Debug.LogError("Invalid dial range.", this);
            return;
        }

        List<string> doorIds = CollectDoorIds();

        // Every door needs one code; every light needs two.
        int requiredCodes = doorIds.Count + layout.Lights.Count * 2;

        long possibleCodes =
            ((long)dialMaximum - dialMinimum + 1)
            * 16
            * (1L << (ControlCode.DirectionCount * 2));

        if (requiredCodes > possibleCodes)
        {
            Debug.LogError(
                $"Need {requiredCodes} codes, but only {possibleCodes} exist.",
                this
            );
            return;
        }

        // Verify that each light has a unique ID.
        HashSet<int> lightIds = new();

        foreach (MazeLightData light in layout.Lights)
        {
            if (!lightIds.Add(light.id))
            {
                Debug.LogError($"Duplicate light ID: {light.id}", this);
                return;
            }
        }

        HashSet<ControlCode> usedCodes = new();

        // Assign one code to each door.
        foreach (string doorId in doorIds)
        {
            ControlCode code = GenerateUniqueCode(usedCodes);

            doorCodes.Add(doorId, code);

            codeActions.Add(
                code,
                new ControlCodeMatch(
                    ControlCodeAction.ToggleDoor,
                    doorId,
                    -1
                )
            );
        }

        // Assign a toggle code and an overload code to each light.
        foreach (MazeLightData light in layout.Lights)
        {
            ControlCode toggleCode = GenerateUniqueCode(usedCodes);
            ControlCode overloadCode = GenerateUniqueCode(usedCodes);

            lightCodes.Add(
                light.id,
                new LightCodePair(toggleCode, overloadCode)
            );

            codeActions.Add(
                toggleCode,
                new ControlCodeMatch(
                    ControlCodeAction.ToggleLight,
                    null,
                    light.id
                )
            );

            codeActions.Add(
                overloadCode,
                new ControlCodeMatch(
                    ControlCodeAction.OverloadLight,
                    null,
                    light.id
                )
            );
        }

        Debug.Log(
            $"Generated {doorCodes.Count} door codes and " +
            $"{lightCodes.Count * 2} light codes " +
            $"for maze layout {layout.LayoutId}.",
            this
        );

        CodesRegenerated?.Invoke();
    }


    private ControlCode GenerateUniqueCode(HashSet<ControlCode> usedCodes)
    {
        while (true)
        {
            ControlDirection[] directions =
                new ControlDirection[ControlCode.DirectionCount];

            for (int i = 0; i < directions.Length; i++)
            {
                directions[i] = (ControlDirection)random.Next(0, 4);
            }

            ControlCode code = new ControlCode(
                random.Next(dialMinimum, dialMaximum + 1),
                random.Next(0, 16),
                directions
            );

            // Add returns false if this combination already exists.
            if (usedCodes.Add(code))
                return code;
        }
    }

    private List<string> CollectDoorIds()
    {
        List<string> ids = new();

        // Vertical doors.
        for (int y = 0; y < layout.Height; y++)
        {
            for (int x = 0; x <= layout.Width; x++)
            {
                if (layout.GetVerticalEdge(x, y) == MazeEdgeType.Door)
                    ids.Add($"V_{x}_{y}");
            }
        }

        // Horizontal doors.
        for (int y = 0; y <= layout.Height; y++)
        {
            for (int x = 0; x < layout.Width; x++)
            {
                if (layout.GetHorizontalEdge(x, y) == MazeEdgeType.Door)
                    ids.Add($"H_{x}_{y}");
            }
        }

        return ids;
    }

    // Used by NavigatorMapHover to display a door's code.
    public bool TryGetDoorCode(string doorId, out ControlCode code)
    {
        return doorCodes.TryGetValue(doorId, out code);
    }

    // Used by NavigatorMapHover to display both light codes.
    public bool TryGetLightCodes(
        int lightId,
        out ControlCode toggleCode,
        out ControlCode overloadCode)
    {
        if (lightCodes.TryGetValue(lightId, out LightCodePair pair))
        {
            toggleCode = pair.Toggle;
            overloadCode = pair.Overload;
            return true;
        }

        toggleCode = default;
        overloadCode = default;
        return false;
    }

    // Used when the Navigator confirms an entered combination.
    public bool TryResolveCode(
        ControlCode enteredCode,
        out ControlCodeMatch match)
    {
        return codeActions.TryGetValue(enteredCode, out match);
    }

    // Temporary debugging command.
    [ContextMenu("Debug/Test First Assigned Code")]
    private void DebugTestFirstCode()
    {
        foreach (var entry in codeActions)
        {
            if (TryResolveCode(entry.Key, out ControlCodeMatch match))
            {
                Debug.Log(
                    $"Entered [{entry.Key}] -> " +
                    $"{match.Action}, " +
                    $"Door: {match.DoorId}, Light: {match.LightId}",
                    this
                );
            }

            return;
        }

        Debug.LogWarning("No codes generated yet.", this);
    }
}
