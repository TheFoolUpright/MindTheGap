using UnityEngine;

[RequireComponent(typeof(CutPath))]
[RequireComponent(typeof(CuttingProgress))]
public class CuttingSeamUI : MonoBehaviour
{
    [System.Serializable]
    public class SideUI
    {
        public Canvas canvas;
        public RectTransform cutA;
        public RectTransform cutB;
        public RectTransform marker;
    }

    [Header("Sides - In Path Order")]
    [SerializeField] private SideUI front = new SideUI();
    [SerializeField] private SideUI right = new SideUI();
    [SerializeField] private SideUI back = new SideUI();
    [SerializeField] private SideUI left = new SideUI();

    [Header("Visibility")]
    [SerializeField] private CutterTool cutter;
    [SerializeField] private float visibleDistance = 2f;
    [SerializeField] private bool alwaysVisible = false;

    private CutPath path;
    private CuttingProgress progress;
    private SideUI[] sides;

    private void Awake()
    {
        path = GetComponent<CutPath>();
        progress = GetComponent<CuttingProgress>();

        sides = new SideUI[] { front, right, back, left };

        foreach (SideUI side in sides)
        {
            if (side.canvas == null ||
                side.cutA == null ||
                side.cutB == null ||
                side.marker == null)
            {
                Debug.LogError(
                    "CuttingSeamUI: Assign all four sides in Inspector.",
                    this
                );

                enabled = false;
                return;
            }
        }
    }

    private void LateUpdate()
    {
        bool visible = alwaysVisible;

        // Show the seam when the held cutter is nearby.
        if (!visible && cutter != null && cutter.IsHeldByHand)
        {
            float distance = Vector3.Distance(
                cutter.transform.position,
                path.transform.position
            );

            visible = distance <= visibleDistance;
        }

        foreach (SideUI side in sides)
            side.canvas.enabled = visible;

        if (!visible)
            return;

        float perimeter = path.GetLength();

        if (perimeter <= 0.00001f)
            return;

        float cutLength = progress.Progress * perimeter;
        float cutStart = progress.StartDistance;

        // Represent either cutting direction as a forward interval.
        if (progress.Direction < 0)
            cutStart -= cutLength;

        cutStart = Mathf.Repeat(cutStart, perimeter);
        float cutEnd = cutStart + cutLength;

        float frontier = Mathf.Repeat(
            progress.StartDistance + progress.Direction * cutLength,
            perimeter
        );

        float sideStart = 0f;

        for (int i = 0; i < sides.Length; i++)
        {
            SideUI side = sides[i];

            float sideLength = Vector3.Distance(
                path.GetCorner(i),
                path.GetCorner((i + 1) % 4)
            );

            if (sideLength <= 0.00001f)
                continue;

            float sideEnd = sideStart + sideLength;

            RectTransform canvasRect =
                (RectTransform)side.canvas.transform;

            float uiWidth = canvasRect.rect.width;

            // The second interval handles wrapping past perimeter zero.
            DrawSection(
                side.cutA, cutStart, cutEnd,
                sideStart, sideLength, uiWidth
            );

            DrawSection(
                side.cutB, cutStart, cutEnd,
                sideStart + perimeter, sideLength, uiWidth
            );

            bool showMarker =
                progress.HasStarted &&
                !progress.IsCompleted &&
                frontier >= sideStart &&
                frontier < sideEnd;

            side.marker.gameObject.SetActive(showMarker);

            if (showMarker)
            {
                float fraction = (frontier - sideStart) / sideLength;

                side.marker.anchoredPosition = new Vector2(
                    -uiWidth * 0.5f + fraction * uiWidth,
                    0f
                );
            }

            sideStart = sideEnd;
        }
    }

    // Draws the overlap between the cut and one side.
    private void DrawSection(
        RectTransform image,
        float cutStart,
        float cutEnd,
        float sideStart,
        float sideLength,
        float uiWidth)
    {
        float start = Mathf.Max(cutStart, sideStart);
        float end = Mathf.Min(cutEnd, sideStart + sideLength);

        if (end <= start)
        {
            image.gameObject.SetActive(false);
            return;
        }

        image.gameObject.SetActive(true);

        float startFraction = (start - sideStart) / sideLength;
        float lengthFraction = (end - start) / sideLength;

        image.anchoredPosition = new Vector2(
            startFraction * uiWidth,
            0f
        );

        image.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            lengthFraction * uiWidth
        );
    }

    private void OnDisable()
    {
        if (sides == null)
            return;

        foreach (SideUI side in sides)
        {
            if (side.canvas != null)
                side.canvas.enabled = false;
        }
    }
}