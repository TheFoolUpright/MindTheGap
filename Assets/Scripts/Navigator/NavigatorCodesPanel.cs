using TMPro;
using UnityEngine;

/// <summary>
/// Displays information about the Navigator map node currently under the cursor.
/// This is a read-only prototype: sending control codes will be added later.
/// </summary>
public class NavigatorCodesPanel : MonoBehaviour
{
    [SerializeField] private TMP_Text codesText;
    [TextArea]
    [SerializeField] private string idleMessage = "Hover over a door or light to inspect its codes.";

    private NavigatorMapNode currentNode;

    private void Awake()
    {
        Clear();
    }

    public void ShowDoor(NavigatorMapNode sender, string edgeKey, int controlCode)
    {
        currentNode = sender;
        if (codesText == null) return;
        codesText.text = "DOOR  " + edgeKey + "\n" +
                         "Control code: " + FormatCode(controlCode);
    }

    public void ShowLight(NavigatorMapNode sender, int lightId, int controlCode,
                          int overloadCode, NavigatorLightState state)
    {
        currentNode = sender;
        if (codesText == null) return;
        codesText.text = "LIGHT  " + lightId + "\n" +
                         "Control code: " + FormatCode(controlCode) + "\n" +
                         "Overload code: " + FormatCode(overloadCode) + "\n" +
                         "Status: " + state;
    }

    public void ClearFrom(NavigatorMapNode sender)
    {
        // Don't clear a newly hovered node when an older node receives PointerExit.
        if (sender == currentNode) Clear();
    }

    public void Clear()
    {
        currentNode = null;
        if (codesText != null)
            codesText.text = idleMessage;
    }

    private static string FormatCode(int code)
    {
        return code < 0 ? "Not assigned yet" : code.ToString();
    }
}
