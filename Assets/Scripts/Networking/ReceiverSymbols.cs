
public static class ReceiverSymbols
{
    // Receiver symbol identifiers
    public const string Light = "LIGHT";
    public const string Door = "DOOR";
    public const string Cancel = "CANCEL";
    public const string Approve = "APPROVE";
    public const string Enemy = "ENEMY";
    public const string Return = "RETURN";
    public const string Cube = "CUBE";
    public const string Pyramid = "PYRAMID";
    public const string Disapprove = "DISAPPROVE";

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
}
