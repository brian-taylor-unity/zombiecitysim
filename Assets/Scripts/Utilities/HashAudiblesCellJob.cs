using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashAudiblesCellJob : IJobEntity
{
    public int CellSize;
    public NativeParallelHashMap<uint, int>.ParallelWriter ParallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, [ReadOnly] in Audible audible)
    {
        var hash = math.hash(audible.GridPositionValue / CellSize);
        ParallelWriter.TryAdd(hash, entityIndexInQuery);
    }
}
