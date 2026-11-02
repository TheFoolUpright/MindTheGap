using UnityEngine;

/// <summary>
/// Optional fixed-zoom map panning. Connect UI Button.onClick to PanNorth,
/// PanSouth, PanEast, PanWest. A physical controller can later call PanBy().
/// </summary>
public class NavigatorMapPan : MonoBehaviour
{
    [SerializeField] private RectTransform viewport;
    [SerializeField] private RectTransform mapContent;
    [SerializeField, Min(1f)] private float pixelsPerPress = 64f;

    public void PanNorth() => PanBy(new Vector2(0f, -pixelsPerPress));
    public void PanSouth() => PanBy(new Vector2(0f, pixelsPerPress));
    public void PanEast()  => PanBy(new Vector2(-pixelsPerPress, 0f));
    public void PanWest()  => PanBy(new Vector2(pixelsPerPress, 0f));

    public void PanBy(Vector2 contentOffset)
    {
        if (viewport == null || mapContent == null) return;
        Vector2 max = new Vector2(
            Mathf.Max(0f, (mapContent.rect.width - viewport.rect.width) * 0.5f),
            Mathf.Max(0f, (mapContent.rect.height - viewport.rect.height) * 0.5f));
        Vector2 p = mapContent.anchoredPosition + contentOffset;
        mapContent.anchoredPosition = new Vector2(
            Mathf.Clamp(p.x, -max.x, max.x),
            Mathf.Clamp(p.y, -max.y, max.y));
    }

    public void CenterMap()
    {
        if (mapContent != null)
            mapContent.anchoredPosition = Vector2.zero;
    }
}
