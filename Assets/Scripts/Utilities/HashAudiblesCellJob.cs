using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashAudiblesCellJob : IJobEntity
{
    public int CellSize;
    public NativeParallelHashMap<int, int>.ParallelWriter ParallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in Audible audible)
    {
        var hash = (int)math.hash(audible.GridPositionValue / CellSize);
        ParallelWriter.TryAdd(hash, entityIndexInQuery);
    }
}
