using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

public class NavigatorMapCursorPan : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private RectTransform mapViewport;
    [SerializeField] private RectTransform mapContent;
    [SerializeField] private RectTransform mapCursor;

    [Header("Cursor Settings")]
    [SerializeField, Min(0.1f)] private float mouseSensitivity = 2f;
    [SerializeField, Min(0f)] private float cursorPadding = 10f;

    [Header("Panning Settings")]
    [SerializeField, Min(1f)] private float panSpeed = 500f;
    [SerializeField, Min(1f)] private float edgeZone = 75f;

    [Header("Initial Map Focus")]
    [SerializeField] private NavigatorMapBuilder mapBuilder;


    [SerializeField, Range(0f, 1f)]
    private float entranceVerticalPlacement = 0.75f;

    private Vector2 cursorPosition;
    private bool cursorCaptured;


    private IEnumerator Start()
    {
        if (mapViewport == null ||
            mapContent == null ||
            mapCursor == null ||
            mapBuilder == null)
        {
            Debug.LogError(
                "Assign the map UI and builder references.",
                this
            );

            enabled = false;
            yield break;
        }

        // Start the virtual cursor in the center.
        cursorPosition = Vector2.zero;
        MoveCursor(Vector2.zero);

        // Wait for Unity to finish laying out the UI.
        yield return new WaitForEndOfFrame();

        Canvas.ForceUpdateCanvases();

        FocusOnEntrance();
    }


    private void Update()
    {
        Mouse mouse = Mouse.current;

        // Click inside the Game view to capture mouse movement.
        if (!cursorCaptured)
        {
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
                CaptureCursor();

            return;
        }

        // Escape releases the mouse so we can use the Unity Editor.
        if ((Keyboard.current != null &&
             Keyboard.current.escapeKey.wasPressedThisFrame) ||
            Cursor.lockState != CursorLockMode.Locked)
        {
            ReleaseCursor();
            return;
        }

        if (mouse != null)
        {
            Vector2 movement = mouse.delta.ReadValue();
            MoveCursor(movement * mouseSensitivity);
        }

        UpdatePanning();
    }

    // Receives movement in local UI units.
    // Later, our physical joystick can call this method too.
    public void MoveCursor(Vector2 movement)
    {
        Vector2 halfSize = mapViewport.rect.size * 0.5f;

        cursorPosition += movement;

        // Calculate the distance from the center to each boundary.
        float maxX = Mathf.Max(0f, halfSize.x - cursorPadding);
        float maxY = Mathf.Max(0f, halfSize.y - cursorPadding);

        // Keep the cursor within the screen.
        cursorPosition.x = Mathf.Clamp(
            cursorPosition.x,
            -maxX,
            maxX
        );

        cursorPosition.y = Mathf.Clamp(
            cursorPosition.y,
            -maxY,
            maxY
        );

        mapCursor.anchoredPosition = cursorPosition;
    }

    private void UpdatePanning()
    {
        Vector2 viewportSize = mapViewport.rect.size;
        Vector2 direction = Vector2.zero;
        Vector2 halfSize = viewportSize * 0.5f;

        if (cursorPosition.x <= -halfSize.x + edgeZone)
            direction.x = -1f;
        else if (cursorPosition.x >= halfSize.x - edgeZone)
            direction.x = 1f;

        if (cursorPosition.y <= -halfSize.y + edgeZone)
            direction.y = -1f;
        else if (cursorPosition.y >= halfSize.y - edgeZone)
            direction.y = 1f;

        direction = Vector2.ClampMagnitude(direction, 1f);

        // Move the map opposite the direction we're looking.
        Vector2 newPosition = mapContent.anchoredPosition
                              - direction * panSpeed * Time.deltaTime;

        newPosition.x = ClampMapAxis(
            newPosition.x,
            viewportSize.x,
            mapContent.rect.width
        );

        newPosition.y = ClampMapAxis(
            newPosition.y,
            viewportSize.y,
            mapContent.rect.height
        );

        mapContent.anchoredPosition = newPosition;
    }

    private void CaptureCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cursorCaptured = true;
    }

    private void ReleaseCursor()
    {
        if (!cursorCaptured)
            return;

        cursorCaptured = false;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            ReleaseCursor();
    }

    private void OnDisable()
    {
        ReleaseCursor();
    }


    public void FocusOnEntrance()
    {
        MazeLayoutData layout = mapBuilder.Layout;

        if (layout == null || !layout.HasEntrance)
        {
            Debug.LogWarning(
                "Selected maze layout has no imported entrance.",
                this
            );
            return;
        }

        Vector2Int entranceCell = layout.EntranceCell;

        if (entranceCell.x < 0 ||
            entranceCell.x >= layout.Width ||
            entranceCell.y < 0 ||
            entranceCell.y >= layout.Height)
        {
            Debug.LogWarning(
                $"Entrance cell {entranceCell} is outside the maze.",
                this
            );
            return;
        }

        float units = mapBuilder.UnitsPerCell;

        // Entrance center in map UI coordinates.
        Vector2 entrancePosition = new Vector2(
            (entranceCell.x + 0.5f) * units,
            (entranceCell.y + 0.5f) * units
        );

        Vector2 viewportSize = mapViewport.rect.size;
        Vector2 contentSize = mapContent.rect.size;

        // Desired entrance position inside the visible viewport.
        Vector2 desiredPosition = new Vector2(
            viewportSize.x * 0.5f,
            viewportSize.y * entranceVerticalPlacement
        );

        Vector2 offset = desiredPosition - entrancePosition;

        // Respect the map's panning boundaries.
        offset.x = ClampMapAxis(
            offset.x,
            viewportSize.x,
            contentSize.x
        );

        offset.y = ClampMapAxis(
            offset.y,
            viewportSize.y,
            contentSize.y
        );

        mapContent.anchoredPosition = offset;

        Debug.Log(
            $"Map focused on entrance cell {entranceCell}.",
            this
        );
    }

    private float ClampMapAxis(
        float position,
        float viewportSize,
        float contentSize)
    {
        // Center a map that's smaller than its viewport.
        if (contentSize <= viewportSize)
            return (viewportSize - contentSize) * 0.5f;

        return Mathf.Clamp(
            position,
            viewportSize - contentSize,
            0f
        );
    }

}
