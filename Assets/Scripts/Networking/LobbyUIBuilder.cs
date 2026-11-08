
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// Small helpers that build uGUI widgets from code for the world-space lobby menu.
public static class LobbyUIBuilder
{
    public static readonly Color PanelColor = new Color(0.08f, 0.09f, 0.12f, 0.95f);
    public static readonly Color ButtonColor = new Color(0.22f, 0.25f, 0.32f, 1f);
    public static readonly Color SelectedColor = new Color(0.20f, 0.45f, 0.75f, 1f);
    public static readonly Color FieldColor = new Color(0.95f, 0.95f, 0.95f, 1f);

    public static RectTransform CreateRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.layer = 5; // UI
        var rect = (RectTransform)go.transform;
        rect.SetParent(parent, false);
        return rect;
    }

    public static void Stretch(RectTransform rect, float inset = 0f)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(inset, inset);
        rect.offsetMax = new Vector2(-inset, -inset);
    }

    public static void SetHeight(GameObject go, float height)
    {
        var layout = go.GetComponent<LayoutElement>();
        if (layout == null)
            layout = go.AddComponent<LayoutElement>();

        layout.preferredHeight = height;
        layout.minHeight = height;
    }

    public static RectTransform Group(Transform parent, string name, float spacing = 12f)
    {
        RectTransform rect = CreateRect(name, parent);
        var layout = rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return rect;
    }

    public static RectTransform Row(Transform parent, string name, float height, float spacing = 12f)
    {
        RectTransform rect = CreateRect(name, parent);
        var layout = rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = true;
        SetHeight(rect.gameObject, height);
        return rect;
    }

    public static TextMeshProUGUI Label(
        Transform parent,
        string text,
        float fontSize = 30f,
        FontStyles style = FontStyles.Normal,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left)
    {
        RectTransform rect = CreateRect("Label", parent);
        var label = rect.gameObject.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = fontSize;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = Color.white;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.raycastTarget = false;
        return label;
    }

    public static UnityEngine.UI.Button Button(
        Transform parent,
        string text,
        UnityAction onClick,
        out TextMeshProUGUI label,
        float height = 70f)
    {
        RectTransform rect = CreateRect("Button: " + text, parent);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = ButtonColor;

        var button = rect.gameObject.AddComponent<UnityEngine.UI.Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.highlightedColor = new Color(1.25f, 1.25f, 1.25f, 1f);
        colors.pressedColor = new Color(0.8f, 0.8f, 0.8f, 1f);
        colors.disabledColor = new Color(0.5f, 0.5f, 0.5f, 0.5f);
        button.colors = colors;
        button.onClick.AddListener(onClick);

        SetHeight(rect.gameObject, height);

        label = Label(rect, text, 30f, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }

    public static UnityEngine.UI.Button Button(Transform parent, string text, UnityAction onClick, float height = 70f)
    {
        return Button(parent, text, onClick, out _, height);
    }

    public static TMP_InputField InputField(Transform parent, string placeholderText, float height = 70f)
    {
        RectTransform rect = CreateRect("InputField", parent);
        // Build inactive so TMP_InputField only enables once fully wired.
        rect.gameObject.SetActive(false);
        var image = rect.gameObject.AddComponent<Image>();
        image.color = FieldColor;
        SetHeight(rect.gameObject, height);

        RectTransform viewport = CreateRect("Text Area", rect);
        Stretch(viewport, 10f);
        viewport.gameObject.AddComponent<RectMask2D>();

        TextMeshProUGUI placeholder = Label(viewport, placeholderText, 30f, FontStyles.Italic);
        placeholder.color = new Color(0.4f, 0.4f, 0.4f, 1f);
        placeholder.textWrappingMode = TextWrappingModes.NoWrap;
        placeholder.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(placeholder.rectTransform);

        TextMeshProUGUI text = Label(viewport, "", 30f);
        text.color = Color.black;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        Stretch(text.rectTransform);

        var field = rect.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = image;
        field.textViewport = viewport;
        field.textComponent = text;
        field.placeholder = placeholder;
        field.lineType = TMP_InputField.LineType.SingleLine;
        rect.gameObject.SetActive(true);
        return field;
    }
}
