
using System;
using Unity.Collections;
using Unity.Netcode;

public enum PlayerRole : byte
{
    Explorer,
    Navigator
}

public struct LobbyEntry : INetworkSerializable,
    IEquatable<LobbyEntry>
{
    public ulong ClientId;
    public FixedString32Bytes Name;
    public PlayerRole Role;
    public bool Ready;

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref ClientId);
        serializer.SerializeValue(ref Name);
        serializer.SerializeValue(ref Role);
        serializer.SerializeValue(ref Ready);
    }

    public bool Equals(LobbyEntry other)
    {
        return ClientId == other.ClientId
            && Name.Equals(other.Name)
            && Role == other.Role
            && Ready == other.Ready;
    }
}
