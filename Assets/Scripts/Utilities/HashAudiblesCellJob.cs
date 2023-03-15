using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashAudiblesCellJob : IJobEntity
{
    public int cellSize;
    public NativeParallelHashMap<int, int>.ParallelWriter parallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in Audible audible)
    {
        var hash = (int)math.hash(audible.GridPositionValue / cellSize);
        parallelWriter.TryAdd(hash, entityIndexInQuery);
    }
}
