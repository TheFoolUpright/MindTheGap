
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NavigatorMapHover : MonoBehaviour
{
    // Text sizes for each part of the panel.
    private const int TitleSize = 34;
    private const int StatusSize = 24;
    private const int SectionSize = 22;
    private const int LabelSize = 18;
    private const int ValueSize = 34;
    private const int MessageSize = 22;
    private const int SpacerSize = 14;

    // Column positions, as a percentage of the text area's width.
    private const string SwitchesColumn = "<pos=20%>";
    private const string DirectionsColumn = "<pos=58%>";
    private const string StatusColumn = "<pos=76%>";

    // The divider is drawn with characters. It should span the text area:
    // DividerLength x DividerCharacterWidth = width of the text area.
    private const int DividerLength = 56;
    private const int DividerCharacterWidth = 8;

    [Header("Map References")]
    [SerializeField] private Canvas mapCanvas;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private RectTransform mapCursor;

    [Header("Codes Display")]
    [SerializeField] private TMP_Text codesText;
    [SerializeField] private NavigatorControlCodeManager codeManager;

    [Header("Text Colors")]
    [SerializeField] private Color titleColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    [SerializeField] private Color sectionColor = new Color32(0x7F, 0xC8, 0xE8, 0xFF);
    [SerializeField] private Color valueColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    [SerializeField] private Color mutedColor = new Color32(0x9A, 0xA5, 0xB1, 0xFF);
    [SerializeField] private Color dividerColor = new Color32(0x3A, 0x42, 0x4C, 0xFF);

    [Header("Light State Colors")]
    [SerializeField] private Color lightOnColor = new Color32(0x8F, 0xDC, 0x8F, 0xFF);
    [SerializeField] private Color lightOffColor = new Color32(0x9A, 0xA5, 0xB1, 0xFF);
    [SerializeField] private Color lightBrokenColor = new Color32(0xFF, 0x8A, 0x80, 0xFF);

    private PointerEventData pointerData;
    private readonly List<RaycastResult> results = new();

    private NavigatorDoorNode currentDoor;
    private MazeDoorState displayedDoorState;
    private NavigatorLightNode currentLight;
    private MazeLightState displayedLightState;
    private bool refreshRequested;

    private void Start()
    {
        if (mapCanvas == null ||
            graphicRaycaster == null ||
            mapCursor == null ||
            codesText == null ||
            EventSystem.current == null)
        {
            Debug.LogError(
                "NavigatorMapHover is missing references or an EventSystem.",
                this
            );
            enabled = false;
            return;
        }

        if (mapCanvas.renderMode != RenderMode.ScreenSpaceOverlay &&
            mapCanvas.worldCamera == null)
        {
            Debug.LogError(
                "Assign the UI camera to MapCanvas's Event Camera field.",
                this
            );
            enabled = false;
            return;
        }

        pointerData = new PointerEventData(EventSystem.current);

        ShowCenteredMessage("CONTROL CODES", "Hover over a map node");
    }


    private void Update()
    {
        Camera eventCamera =
            mapCanvas.renderMode == RenderMode.ScreenSpaceOverlay
                ? null
                : mapCanvas.worldCamera;

        // Convert our virtual cursor into a screen position.
        pointerData.position =
            RectTransformUtility.WorldToScreenPoint(
                eventCamera,
                mapCursor.position
            );

        results.Clear();
        graphicRaycaster.Raycast(pointerData, results);

        NavigatorDoorNode hoveredDoor = null;
        NavigatorLightNode hoveredLight = null;

        foreach (RaycastResult result in results)
        {
            // First, check whether the cursor is over a door.
            hoveredDoor =
                result.gameObject.GetComponentInParent<NavigatorDoorNode>();

            if (hoveredDoor != null)
                break;

            // Otherwise, check whether it is over a light.
            hoveredLight =
                result.gameObject.GetComponentInParent<NavigatorLightNode>();

            if (hoveredLight != null)
                break;
        }

        // Has the cursor moved to another node?
        bool hoverChanged =
            hoveredDoor != currentDoor ||
            hoveredLight != currentLight;

        // Has the currently hovered light changed state?
        bool lightStateChanged =
            hoveredLight != null &&
            hoveredLight.CurrentState != displayedLightState;

        // Has the currently hovered door changed state?
        bool doorStateChanged =
            hoveredDoor != null &&
            hoveredDoor.CurrentState != displayedDoorState;

        if (!hoverChanged && !lightStateChanged && !doorStateChanged && !refreshRequested)
            return;

        refreshRequested = false;
        currentDoor = hoveredDoor;
        currentLight = hoveredLight;

        if (currentLight != null)
            displayedLightState = currentLight.CurrentState;

        if (currentDoor != null)
            displayedDoorState = currentDoor.CurrentState;

        UpdateCodesDisplay();
    }




    private void UpdateCodesDisplay()
    {
        if (codeManager == null)
        {
            ShowCenteredMessage("CONTROL CODES", "Code Manager not assigned");
            return;
        }

        // Display information for the hovered light.
        if (currentLight != null)
        {
            int lightId = currentLight.LightId;
            string header = FormatHeader(
                $"LIGHT {lightId}",
                FormatLightState(currentLight.CurrentState)
            );

            if (codeManager.TryGetLightCodes(
                lightId,
                out ControlCode toggleCode,
                out ControlCode overloadCode))
            {
                ShowDetails(
                    header +
                    FormatCodeSection("TOGGLE CODE", toggleCode) +
                    FormatSpacer() +
                    FormatCodeSection("OVERLOAD CODE", overloadCode)
                );
            }
            else
            {
                ShowDetails(header + FormatMissingCodes("Codes not assigned"));
            }

            return;
        }

        // Display information for the hovered door.
        if (currentDoor != null)
        {
            string doorId = currentDoor.DoorId;
            string header = FormatHeader(
                $"DOOR {doorId}",
                FormatDoorState(currentDoor.CurrentState)
            );

            if (codeManager.TryGetDoorCode(
                doorId,
                out ControlCode doorCode))
            {
                ShowDetails(header + FormatCodeSection("CONTROL CODE", doorCode));
            }
            else
            {
                ShowDetails(header + FormatMissingCodes("Code not assigned"));
            }

            return;
        }

        // Nothing is currently being hovered.
        ShowCenteredMessage("CONTROL CODES", "Hover over a map node");
    }



    private void ShowDetails(string text)
    {
        codesText.verticalAlignment = VerticalAlignmentOptions.Top;
        codesText.text = text;
    }

    private void ShowCenteredMessage(string title, string message)
    {
        codesText.verticalAlignment = VerticalAlignmentOptions.Middle;
        codesText.text =
            "<align=center>" +
            $"<size={TitleSize}><color={ToHex(titleColor)}><b>{title}</b></color></size>\n" +
            $"<size={MessageSize}><color={ToHex(mutedColor)}>{message}</color></size>" +
            "</align>";
    }

    private string FormatHeader(string title, string status = "")
    {
        string divider = new string('─', DividerLength);

        return $"<size={TitleSize}><color={ToHex(titleColor)}><b>{title}</b></color></size>" +
               $"{status}\n" +
               $"<size={LabelSize}><nobr><mspace={DividerCharacterWidth}>" +
               $"<color={ToHex(dividerColor)}>{divider}</color>" +
               "</mspace></nobr></size>\n";
    }

    private string FormatLightState(MazeLightState state)
    {
        Color color = state switch
        {
            MazeLightState.On => lightOnColor,
            MazeLightState.Broken => lightBrokenColor,
            _ => lightOffColor
        };

        return $"{StatusColumn}<size={StatusSize}><color={ToHex(color)}>" +
               $"{state.ToString().ToUpperInvariant()}</color></size>";
    }
    private string FormatDoorState(MazeDoorState state)
    {
        Color color = state == MazeDoorState.Open
            ? lightOnColor
            : mutedColor;

        return $"{StatusColumn}<size={StatusSize}>" +
               $"<color={ToHex(color)}>" +
               $"{state.ToString().ToUpperInvariant()}" +
               "</color></size>";
    }

    private string FormatCodeSection(string title, ControlCode code)
    {
        string switches = System.Convert
            .ToString(code.ToggleMask, 2)
            .PadLeft(4, '0');

        return $"<size={SectionSize}><color={ToHex(sectionColor)}><b>{title}</b></color></size>\n" +
               $"<size={LabelSize}><color={ToHex(mutedColor)}>" +
               $"Dial{SwitchesColumn}Switches{DirectionsColumn}Directions" +
               "</color></size>\n" +
               $"<size={ValueSize}><color={ToHex(valueColor)}>{code.Dial}" +
               $"{SwitchesColumn}<mspace=0.65em>{switches}</mspace>" +
               $"{DirectionsColumn}<mspace=0.85em>{FormatDirections(code)}</mspace>" +
               "</color></size>\n";
    }

    private string FormatDirections(ControlCode code)
    {
        StringBuilder directions = new();

        for (int i = 0; i < ControlCode.DirectionCount; i++)
        {
            directions.Append(code.GetDirection(i) switch
            {
                ControlDirection.Up => "↑",
                ControlDirection.Right => "→",
                ControlDirection.Down => "↓",
                ControlDirection.Left => "←",
                _ => "?"
            });
        }

        return directions.ToString();
    }

    private string FormatMissingCodes(string message)
    {
        return $"<size={MessageSize}><color={ToHex(mutedColor)}>{message}</color></size>";
    }

    private string FormatSpacer()
    {
        return $"<size={SpacerSize}> </size>\n";
    }


    private string ToHex(Color color)
    {
        return $"#{ColorUtility.ToHtmlStringRGBA(color)}";
    }

    // Lets Inspector color changes show up immediately during Play mode.
    private void OnValidate()
    {
        refreshRequested = true;
    }


    private void OnEnable()
    {
        if (codeManager != null)
            codeManager.CodesRegenerated += UpdateCodesDisplay;
    }

    private void OnDisable()
    {
        if (codeManager != null)
            codeManager.CodesRegenerated -= UpdateCodesDisplay;
    }

}
