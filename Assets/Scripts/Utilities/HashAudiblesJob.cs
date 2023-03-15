using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashAudiblesJob : IJobEntity
{
    public NativeParallelMultiHashMap<int, int3>.ParallelWriter parallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in Audible audible)
    {
        var hash = (int)math.hash(audible.GridPositionValue);
        parallelWriter.Add(hash, audible.Target);
    }
}
