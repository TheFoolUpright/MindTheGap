
using System;

public static class MessageProtocol
{
    
    
    // Message types
    public const string SymbolSend = "SYMBOL_SEND";
    public const string SymbolReceived = "SYMBOL_RECEIVED";

    // Symbol identifiers
    public const string Light = "LIGHT";
    public const string Door = "DOOR";
    public const string Cancel = "CANCEL";
    public const string Approve = "APPROVE";
    public const string Enemy = "ENEMY";
    public const string Return = "RETURN";
    public const string Cube = "CUBE";
    public const string Pyramid = "PYRAMID";
    public const string Disapprove = "DISAPPROVE";

    // Creates a message such as SYMBOL_SEND|LIGHT|
    public static string CreateSymbolMessage(string symbol)
    {
        if (!IsValidSymbol(symbol))
        {
            throw new ArgumentException(
                $"Invalid symbol: {symbol}"
            );
        }

        return $"{SymbolSend}|{symbol}|";
    }

    // Creates a confirmation such as SYMBOL_RECEIVED|LIGHT|
    public static string CreateSymbolConfirmation(string symbol)
    {
        if (!IsValidSymbol(symbol))
        {
            throw new ArgumentException(
                $"Invalid symbol: {symbol}"
            );
        }

        return $"{SymbolReceived}|{symbol}|";
    }

    // Checks whether a symbol is supported.
    public static bool IsValidSymbol(string symbol)
    {
        switch (symbol)
        {
            case Light:
            case Door:
            case Cancel:
            case Approve:
            case Enemy:
            case Return:
            case Cube:
            case Pyramid:
            case Disapprove:
                return true;

            default:
                return false;
        }
    }

    // Attempts to read a symbol message.
    public static bool TryParseSymbolMessage(
        string message,
        out string messageType,
        out string symbol)
    {
        messageType = null;
        symbol = null;

        if (string.IsNullOrEmpty(message))
            return false;

        string[] fields = message.Split('|');

        // Expected: TYPE|SYMBOL|
        if (fields.Length != 3 || fields[2] != "")
            return false;

        if (fields[0] != SymbolSend &&
            fields[0] != SymbolReceived)
        {
            return false;
        }

        if (!IsValidSymbol(fields[1]))
            return false;

        messageType = fields[0];
        symbol = fields[1];

        return true;
    }
}
