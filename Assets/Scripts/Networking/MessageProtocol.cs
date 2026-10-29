
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


    public static string EncodeFurnaceState(FurnaceState state)
    {
        return $"FURNACE|" +
               $"{state.cubeGoal}|" +
               $"{state.pyramidGoal}|" +
               $"{state.totalCubes}|" +
               $"{state.totalPyramids}|" +
               $"{state.pendingCubes}|" +
               $"{state.pendingPyramids}";
    }

    public static bool TryDecodeFurnaceState(
        string message,
        out FurnaceState state)
    {
        state = default;

        if (string.IsNullOrEmpty(message))
            return false;

        string[] parts = message.Split('|');

        // Message type + six integer values
        if (parts.Length != 7 || parts[0] != "FURNACE")
            return false;

        if (!int.TryParse(parts[1], out int cubeGoal) ||
            !int.TryParse(parts[2], out int pyramidGoal) ||
            !int.TryParse(parts[3], out int totalCubes) ||
            !int.TryParse(parts[4], out int totalPyramids) ||
            !int.TryParse(parts[5], out int pendingCubes) ||
            !int.TryParse(parts[6], out int pendingPyramids))
        {
            return false;
        }

        if (cubeGoal <= 0 || pyramidGoal <= 0 ||
            totalCubes < 0 || totalPyramids < 0 ||
            pendingCubes < 0 || pendingPyramids < 0)
        {
            return false;
        }

        state = new FurnaceState
        {
            cubeGoal = cubeGoal,
            pyramidGoal = pyramidGoal,
            totalCubes = totalCubes,
            totalPyramids = totalPyramids,
            pendingCubes = pendingCubes,
            pendingPyramids = pendingPyramids
        };

        return true;
    }

}
