
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

    private Vector2 cursorPosition;
    private bool cursorCaptured;

    private void Start()
    {
        if (mapViewport == null || mapContent == null || mapCursor == null)
        {
            Debug.LogError("Assign the map UI references.", this);
            enabled = false;
            return;
        }

        // Start the cursor in the center of the map display.
        cursorPosition = Vector2.zero;
        MoveCursor(Vector2.zero);
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

        float minX = Mathf.Min(
            0f, viewportSize.x - mapContent.rect.width
        );

        float minY = Mathf.Min(
            0f, viewportSize.y - mapContent.rect.height
        );

        newPosition.x = Mathf.Clamp(newPosition.x, minX, 0f);
        newPosition.y = Mathf.Clamp(newPosition.y, minY, 0f);

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
}
