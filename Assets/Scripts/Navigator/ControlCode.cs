
using System;
using System.Collections.Generic;
using UnityEngine;

public enum ControlDirection
{
    Up,
    Right,
    Down,
    Left
}

[Serializable]
public struct ControlCode : IEquatable<ControlCode>
{
    public const int DirectionCount = 6;

    [SerializeField] private int dial;
    [SerializeField] private int toggleMask;
    [SerializeField] private int directionBits;

    public int Dial => dial;
    public int ToggleMask => toggleMask;

    public ControlCode(
        int dial,
        int toggleMask,
        IReadOnlyList<ControlDirection> directions)
    {
        if (toggleMask < 0 || toggleMask > 15)
            throw new ArgumentOutOfRangeException(nameof(toggleMask));

        if (directions == null || directions.Count != DirectionCount)
            throw new ArgumentException("Exactly six directions are required.");

        this.dial = dial;
        this.toggleMask = toggleMask;
        directionBits = 0;

        for (int i = 0; i < DirectionCount; i++)
        {
            int value = (int)directions[i];

            if (value < 0 || value > 3)
                throw new ArgumentException("Invalid direction.");

            // Store each direction in two consecutive bits.
            directionBits |= value << (i * 2);
        }
    }

    public ControlDirection GetDirection(int index)
    {
        if (index < 0 || index >= DirectionCount)
            throw new ArgumentOutOfRangeException(nameof(index));

        return (ControlDirection)((directionBits >> (index * 2)) & 3);
    }

    public bool Equals(ControlCode other)
    {
        return dial == other.dial
            && toggleMask == other.toggleMask
            && directionBits == other.directionBits;
    }

    public override bool Equals(object obj)
    {
        return obj is ControlCode other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = 17;
            hash = hash * 31 + dial;
            hash = hash * 31 + toggleMask;
            hash = hash * 31 + directionBits;
            return hash;
        }
    }

    public override string ToString()
    {
        string binary = Convert.ToString(toggleMask, 2).PadLeft(4, '0');

        string[] sequence = new string[DirectionCount];

        for (int i = 0; i < DirectionCount; i++)
            sequence[i] = GetDirection(i).ToString();

        return $"Dial: {dial} | Toggles: {binary} | " +
               $"Directions: {string.Join(" → ", sequence)}";
    }
}
