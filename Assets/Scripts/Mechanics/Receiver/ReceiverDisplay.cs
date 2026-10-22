
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class ReceiverDisplay : MonoBehaviour
{
    [System.Serializable]
    private struct SymbolSprite
    {
        public string symbolId;
        public Sprite sprite;
    }

    [Header("References")]
    [SerializeField] private ReceiverController receiverController;
    [SerializeField] private Image[] symbolSlots = new Image[3];

    [Header("Symbol Sprites")]
    [SerializeField] private SymbolSprite[] symbolSprites;

    private readonly Dictionary<string, Sprite> spriteLookup = new();

    private void Awake()
    {
        foreach (SymbolSprite entry in symbolSprites)
        {
            if (string.IsNullOrEmpty(entry.symbolId))
                continue;

            spriteLookup[entry.symbolId] = entry.sprite;
        }
    }

    private void OnEnable()
    {
        if (receiverController == null)
            return;

        receiverController.OnHistoryChanged += UpdateDisplay;

        // Show the current history when enabled.
        UpdateDisplay(receiverController.SymbolHistory);
    }

    private void OnDisable()
    {
        if (receiverController != null)
        {
            receiverController.OnHistoryChanged -= UpdateDisplay;
        }
    }


    private void UpdateDisplay(IReadOnlyList<string> history)
    {
        for (int i = 0; i < symbolSlots.Length; i++)
        {
            Image slot = symbolSlots[i];

            if (slot == null)
                continue;

            // Reverse the history so the oldest message
            // appears at the top and newest at the bottom.
            int historyIndex = symbolSlots.Length - 1 - i;

            if (historyIndex < history.Count &&
                spriteLookup.TryGetValue(
                    history[historyIndex],
                    out Sprite sprite
                ) &&
                sprite != null)
            {
                slot.sprite = sprite;
                slot.enabled = true;
            }
            else
            {
                slot.sprite = null;
                slot.enabled = false;
            }
        }
    }

}
