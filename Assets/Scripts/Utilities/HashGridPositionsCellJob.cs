using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashGridPositionsCellJob : IJobEntity
{
    public int cellSize;
    public NativeParallelHashMap<int, int>.ParallelWriter parallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in GridPosition gridPosition)
    {
        var hash = (int)math.hash(gridPosition.Value / cellSize);
        parallelWriter.TryAdd(hash, entityIndexInQuery);
    }
}
