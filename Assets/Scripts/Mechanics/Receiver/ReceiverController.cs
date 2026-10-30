
using System;
using System.Collections.Generic;
using UnityEngine;

public class ReceiverController : MonoBehaviour
{
    [Header("Receiver Settings")]
    [SerializeField] private int maxHistory = 3;

    // Newest symbol is stored at index 0.
    private readonly List<string> symbolHistory = new();

    // UI components can subscribe to this event.
    public event Action<IReadOnlyList<string>> OnHistoryChanged;

    // Explorer notification systems can subscribe to this event.
    public event Action<string> OnSymbolReceived;

    public IReadOnlyList<string> SymbolHistory => symbolHistory.AsReadOnly();

    public void AddSymbol(string symbol)
    {
        if (!ReceiverSymbols.IsValidSymbol(symbol))
        {
            Debug.LogWarning($"Invalid receiver symbol: {symbol}");
            return;
        }

        // Insert the newest symbol at the beginning.
        symbolHistory.Insert(0, symbol);

        // Remove the oldest symbol if the history is full.
        if (symbolHistory.Count > maxHistory)
        {
            symbolHistory.RemoveAt(symbolHistory.Count - 1);
        }

        Debug.Log($"Receiver added symbol: {symbol}");

        // Notify the UI that the displayed history changed.
        OnHistoryChanged?.Invoke(SymbolHistory);

        // Notify sound and vibration systems.
        OnSymbolReceived?.Invoke(symbol);
    }

    public void ClearHistory()
    {
        symbolHistory.Clear();
        OnHistoryChanged?.Invoke(SymbolHistory);
    }
}
