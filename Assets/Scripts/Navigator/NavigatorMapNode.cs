using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum NavigatorMapNodeKind : byte { Door, Light }
public enum NavigatorLightState : byte { On, Off, Broken }

/// <summary>
/// Attach to the root of each Door and Light UI prefab.
/// A raycast-enabled circle Image in the Node child receives pointer input,
/// which bubbles to this component on the root.
/// </summary>
public class NavigatorMapNode : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("UI references")]
    [SerializeField] private Image nodeBackground; // circular child named Node
    [SerializeField] private Image segmentImage;   // Door prefab only: child named Segment
    [SerializeField] private Image iconImage;      // optional child icon

    [Header("Optional light sprites")]
    [SerializeField] private Sprite lightOnSprite;
    [SerializeField] private Sprite lightOffSprite;
    [SerializeField] private Sprite lightBrokenSprite;

    [Header("Light colors")]
    [SerializeField] private Color lightOnColor = new Color(1f, 0.78f, 0.18f, 1f);
    [SerializeField] private Color lightOffColor = new Color(0.38f, 0.48f, 0.61f, 1f);
    [SerializeField] private Color lightBrokenColor = new Color(0.85f, 0.21f, 0.25f, 1f);

    [Header("Runtime / test state")]
    [SerializeField] private NavigatorLightState lightState = NavigatorLightState.On;
    [SerializeField, HideInInspector] private NavigatorMapNodeKind kind;
    [SerializeField, HideInInspector] private string edgeKey;
    [SerializeField, HideInInspector] private int lightId = -1;
    [SerializeField, HideInInspector] private int controlCode = -1;
    [SerializeField, HideInInspector] private int overloadCode = -1;

    private NavigatorCodesPanel codesPanel;
    private bool isHovered;

    // Rebind after entering Play Mode, including when the map is loaded as a prefab.
    public void BindCodesPanel(NavigatorCodesPanel panel) => codesPanel = panel;

    public bool CanRenderDoor => nodeBackground != null && segmentImage != null;
    public bool CanRenderLight => nodeBackground != null;
    public NavigatorLightState LightState => lightState;
    public string DoorKey => edgeKey;
    public int LightId => lightId;

    public void ConfigureDoor(string key, bool vertical, float segmentLength,
                              float thickness, NavigatorCodesPanel panel)
    {
        kind = NavigatorMapNodeKind.Door;
        edgeKey = key;
        lightId = -1;
        controlCode = -1; // Existing MazeLayoutData has NO door code metadata yet.
        overloadCode = -1;
        codesPanel = panel;

        if (segmentImage != null)
        {
            RectTransform line = segmentImage.rectTransform;
            Center(line);
            line.sizeDelta = vertical
                ? new Vector2(thickness, segmentLength)
                : new Vector2(segmentLength, thickness);
            segmentImage.raycastTarget = false;
        }

        SetNodeRaycast();
    }

    public void ConfigureLight(int id, int onOffCode, NavigatorCodesPanel panel)
    {
        kind = NavigatorMapNodeKind.Light;
        lightId = id;
        edgeKey = null;
        controlCode = onOffCode;
        overloadCode = -1; // To be imported from Tiled later.
        codesPanel = panel;
        SetNodeRaycast();
        ApplyLightAppearance();
    }

    // Future importer / networking hooks; these do not write to MazeLayoutData.
    public void SetDoorCode(int code)
    {
        if (kind != NavigatorMapNodeKind.Door) return;
        controlCode = code;
        RefreshHoverText();
    }

    public void SetOverloadCode(int code)
    {
        if (kind != NavigatorMapNodeKind.Light) return;
        overloadCode = code;
        RefreshHoverText();
    }

    public void SetLightState(NavigatorLightState state)
    {
        if (kind != NavigatorMapNodeKind.Light) return;
        lightState = state;
        ApplyLightAppearance();
        RefreshHoverText();
    }

    [ContextMenu("Preview Light / On")]
    private void PreviewOn() => SetLightState(NavigatorLightState.On);

    [ContextMenu("Preview Light / Off")]
    private void PreviewOff() => SetLightState(NavigatorLightState.Off);

    [ContextMenu("Preview Light / Broken")]
    private void PreviewBroken() => SetLightState(NavigatorLightState.Broken);

    private void OnEnable()
    {
        if (kind == NavigatorMapNodeKind.Light)
            ApplyLightAppearance();
    }

    private void OnDisable()
    {
        if (isHovered && codesPanel != null)
            codesPanel.ClearFrom(this);
        isHovered = false;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isHovered = true;
        RefreshHoverText();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isHovered = false;
        if (codesPanel != null)
            codesPanel.ClearFrom(this);
    }

    private void RefreshHoverText()
    {
        if (!isHovered || codesPanel == null) return;
        if (kind == NavigatorMapNodeKind.Door)
            codesPanel.ShowDoor(this, edgeKey, controlCode);
        else
            codesPanel.ShowLight(this, lightId, controlCode, overloadCode, lightState);
    }

    private void ApplyLightAppearance()
    {
        if (nodeBackground == null) return;
        Color color;
        Sprite sprite;
        switch (lightState)
        {
            case NavigatorLightState.Off:
                color = lightOffColor;
                sprite = lightOffSprite;
                break;
            case NavigatorLightState.Broken:
                color = lightBrokenColor;
                sprite = lightBrokenSprite;
                break;
            default:
                color = lightOnColor;
                sprite = lightOnSprite;
                break;
        }
        nodeBackground.color = color;
        if (iconImage != null && sprite != null)
            iconImage.sprite = sprite;
    }

    private void SetNodeRaycast()
    {
        if (nodeBackground != null) nodeBackground.raycastTarget = true;
        if (iconImage != null) iconImage.raycastTarget = false;
    }

    private static void Center(RectTransform rt)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.localRotation = Quaternion.identity;
    }
}
