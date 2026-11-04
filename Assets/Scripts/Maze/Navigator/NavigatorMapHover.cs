
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class NavigatorMapHover : MonoBehaviour
{
    [Header("Map References")]
    [SerializeField] private Canvas mapCanvas;
    [SerializeField] private GraphicRaycaster graphicRaycaster;
    [SerializeField] private RectTransform mapCursor;

    [Header("Codes Display")]
    [SerializeField] private TMP_Text codesText;
    [SerializeField] private NavigatorControlCodeManager codeManager;

    private PointerEventData pointerData;
    private readonly List<RaycastResult> results = new();

    private NavigatorDoorNode currentDoor;
    private NavigatorLightNode currentLight;
    private MazeLightState displayedLightState;

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

        codesText.text = "Hover over a map node";
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

        if (!hoverChanged && !lightStateChanged)
            return;

        currentDoor = hoveredDoor;
        currentLight = hoveredLight;

        if (currentLight != null)
            displayedLightState = currentLight.CurrentState;

        UpdateCodesDisplay();
    }




    private void UpdateCodesDisplay()
    {
        if (codeManager == null)
        {
            codesText.text = "Code Manager not assigned";
            return;
        }

        // Display information for the hovered light.
        if (currentLight != null)
        {
            int lightId = currentLight.LightId;

            if (codeManager.TryGetLightCodes(
                lightId,
                out ControlCode toggleCode,
                out ControlCode overloadCode))
            {
                codesText.text =
                    $"LIGHT {lightId}\n" +
                    $"State: {currentLight.CurrentState}\n\n" +
                    $"TOGGLE CODE\n" +
                    $"{FormatCode(toggleCode)}\n\n" +
                    $"OVERLOAD CODE\n" +
                    $"{FormatCode(overloadCode)}";
            }
            else
            {
                codesText.text =
                    $"LIGHT {lightId}\nCodes not assigned";
            }

            return;
        }

        // Display information for the hovered door.
        if (currentDoor != null)
        {
            string doorId = currentDoor.DoorId;

            if (codeManager.TryGetDoorCode(
                doorId,
                out ControlCode doorCode))
            {
                codesText.text =
                    $"DOOR {doorId}\n\n" +
                    $"CONTROL CODE\n" +
                    $"{FormatCode(doorCode)}";
            }
            else
            {
                codesText.text =
                    $"DOOR {doorId}\nCode not assigned";
            }

            return;
        }

        // Nothing is currently being hovered.
        codesText.text = "Hover over a map node";
    }



    private string FormatCode(ControlCode code)
    {
        string binary = System.Convert
            .ToString(code.ToggleMask, 2)
            .PadLeft(4, '0');

        string[] directions = new string[ControlCode.DirectionCount];

        for (int i = 0; i < ControlCode.DirectionCount; i++)
        {
            directions[i] = code.GetDirection(i) switch
            {
                ControlDirection.Up => "↑",
                ControlDirection.Right => "→",
                ControlDirection.Down => "↓",
                ControlDirection.Left => "←",
                _ => "?"
            };
        }

        return $"Dial: {code.Dial} | Switches: {binary}\n" +
               string.Join("  ", directions);
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
