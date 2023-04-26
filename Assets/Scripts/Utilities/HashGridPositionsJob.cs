using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashGridPositionsJob : IJobEntity
{
    public NativeParallelHashMap<uint, int>.ParallelWriter ParallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, [ReadOnly] in GridPosition gridPosition)
    {
        var hash = math.hash(gridPosition.Value);
        ParallelWriter.TryAdd(hash, entityIndexInQuery);
    }
}
