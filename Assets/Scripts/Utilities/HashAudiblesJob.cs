using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashAudiblesJob : IJobEntity
{
    public NativeParallelMultiHashMap<int, int3>.ParallelWriter ParallelWriter;

    public void Execute(in Audible audible)
    {
        var hash = (int)math.hash(audible.GridPositionValue);
        ParallelWriter.Add(hash, audible.Target);
    }
}
