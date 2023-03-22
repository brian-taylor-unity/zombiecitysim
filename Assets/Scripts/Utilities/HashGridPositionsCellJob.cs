using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashGridPositionsCellJob : IJobEntity
{
    public int CellSize;
    public NativeParallelHashMap<int, int>.ParallelWriter ParallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in GridPosition gridPosition)
    {
        var hash = (int)math.hash(gridPosition.Value / CellSize);
        ParallelWriter.TryAdd(hash, entityIndexInQuery);
    }
}
