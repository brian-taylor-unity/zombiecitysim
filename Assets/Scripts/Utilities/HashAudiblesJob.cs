using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashAudiblesJob : IJobEntity
{
    public NativeParallelMultiHashMap<uint, int3>.ParallelWriter ParallelWriter;

    public void Execute(in Audible audible)
    {
        var hash = math.hash(audible.GridPositionValue);
        ParallelWriter.Add(hash, audible.Target);
    }
}
