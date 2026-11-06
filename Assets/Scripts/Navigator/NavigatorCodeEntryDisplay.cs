
using System;
using System.Text;
using TMPro;
using UnityEngine;

public class NavigatorCodeEntryDisplay : MonoBehaviour
{
    private const int LineSize = 30;
    private const string Prompt = ">_";
    private const string EmptySlot = "·";

    [Header("References")]
    [SerializeField] private NavigatorCodeEntry codeEntry;
    [SerializeField] private TMP_Text displayText;

    [Header("Message Flash")]
    [SerializeField, Min(0f)] private float flashSeconds = 1.5f;

    [Header("Colors")]
    [SerializeField] private Color promptColor = new Color32(0x7F, 0xC8, 0xE8, 0xFF);
    [SerializeField] private Color enteredColor = new Color32(0xFF, 0xFF, 0xFF, 0xFF);
    [SerializeField] private Color emptySlotColor = new Color32(0x66, 0x70, 0x7B, 0xFF);
    [SerializeField] private Color acceptedColor = new Color32(0x8F, 0xDC, 0x8F, 0xFF);
    [SerializeField] private Color errorColor = new Color32(0xFF, 0x8A, 0x80, 0xFF);

    private bool isFlashing;
    private float flashEndTime;
    private CodeConfirmResult flashResult;
    private bool refreshRequested;

    private void OnEnable()
    {
        if (codeEntry != null)
        {
            codeEntry.InputChanged += OnInputChanged;
            codeEntry.CodeConfirmed += OnCodeConfirmed;
        }

        UpdateDisplay();
    }

    private void OnDisable()
    {
        if (codeEntry != null)
        {
            codeEntry.InputChanged -= OnInputChanged;
            codeEntry.CodeConfirmed -= OnCodeConfirmed;
        }

        isFlashing = false;
    }

    private void Update()
    {
        if (isFlashing && Time.unscaledTime >= flashEndTime)
        {
            isFlashing = false;
            refreshRequested = true;
        }

        if (!refreshRequested)
            return;

        refreshRequested = false;
        UpdateDisplay();
    }

    // Any new input ends the message and goes straight back to the prompt.
    private void OnInputChanged()
    {
        isFlashing = false;
        UpdateDisplay();
    }

    private void OnCodeConfirmed(CodeConfirmResult result)
    {
        flashResult = result;
        flashEndTime = Time.unscaledTime + flashSeconds;
        isFlashing = true;

        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (codeEntry == null || displayText == null)
            return;

        string content = isFlashing
            ? FormatMessage(flashResult)
            : FormatEnteredCode();

        displayText.text =
            $"<size={LineSize}><nobr>" +
            $"<mspace=0.6em><color={ToHex(promptColor)}>{Prompt}</color> </mspace>" +
            $"{content}</nobr></size>";
    }

    private string FormatEnteredCode()
    {
        string switches = Convert
            .ToString(codeEntry.ToggleMask, 2)
            .PadLeft(4, '0');

        return $"<color={ToHex(enteredColor)}>" +
               $"<mspace=0.6em>{codeEntry.Dial} - {switches} - </mspace>" +
               $"<mspace=0.85em>{FormatDirections()}</mspace>" +
               "</color>";
    }

    private string FormatDirections()
    {
        int count = codeEntry.Directions.Count;
        StringBuilder directions = new();

        for (int i = 0; i < count; i++)
            directions.Append(DirectionToArrow(codeEntry.Directions[i]));

        if (count < ControlCode.DirectionCount)
        {
            directions.Append($"<color={ToHex(emptySlotColor)}>");

            for (int i = count; i < ControlCode.DirectionCount; i++)
                directions.Append(EmptySlot);

            directions.Append("</color>");
        }

        return directions.ToString();
    }

    private string FormatMessage(CodeConfirmResult result)
    {
        return result switch
        {
            CodeConfirmResult.Accepted =>
                $"<color={ToHex(acceptedColor)}>CODE ACCEPTED</color>",
            CodeConfirmResult.Incomplete =>
                $"<color={ToHex(errorColor)}>ENTER ALL 6 DIRECTIONS</color>",
            _ =>
                $"<color={ToHex(errorColor)}>INVALID CODE</color>"
        };
    }

    private string DirectionToArrow(ControlDirection direction)
    {
        return direction switch
        {
            ControlDirection.Up => "↑",
            ControlDirection.Right => "→",
            ControlDirection.Down => "↓",
            ControlDirection.Left => "←",
            _ => "?"
        };
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
}
