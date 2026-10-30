
using System;
using Unity.Netcode;

public struct FurnaceNetworkSnapshot :
    INetworkSerializable,
    IEquatable<FurnaceNetworkSnapshot>
{
    public int cubeGoal;
    public int pyramidGoal;
    public int totalCubes;
    public int totalPyramids;
    public int pendingCubes;
    public int pendingPyramids;

    public void NetworkSerialize<T>(
        BufferSerializer<T> serializer)
        where T : IReaderWriter
    {
        serializer.SerializeValue(ref cubeGoal);
        serializer.SerializeValue(ref pyramidGoal);
        serializer.SerializeValue(ref totalCubes);
        serializer.SerializeValue(ref totalPyramids);
        serializer.SerializeValue(ref pendingCubes);
        serializer.SerializeValue(ref pendingPyramids);
    }

    public bool Equals(FurnaceNetworkSnapshot other)
    {
        return cubeGoal == other.cubeGoal
            && pyramidGoal == other.pyramidGoal
            && totalCubes == other.totalCubes
            && totalPyramids == other.totalPyramids
            && pendingCubes == other.pendingCubes
            && pendingPyramids == other.pendingPyramids;
    }

    public static FurnaceNetworkSnapshot FromState(
        FurnaceState state)
    {
        return new FurnaceNetworkSnapshot
        {
            cubeGoal = state.cubeGoal,
            pyramidGoal = state.pyramidGoal,
            totalCubes = state.totalCubes,
            totalPyramids = state.totalPyramids,
            pendingCubes = state.pendingCubes,
            pendingPyramids = state.pendingPyramids
        };
    }

    public FurnaceState ToState()
    {
        return new FurnaceState
        {
            cubeGoal = cubeGoal,
            pyramidGoal = pyramidGoal,
            totalCubes = totalCubes,
            totalPyramids = totalPyramids,
            pendingCubes = pendingCubes,
            pendingPyramids = pendingPyramids
        };
    }
}
