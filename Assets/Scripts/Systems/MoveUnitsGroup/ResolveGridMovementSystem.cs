using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateAfter(typeof(MoveTowardsTargetSystem))]
public class ResolveGridMovementSystem : JobComponentSystem
{
    private EntityQuery query;
    private NativeMultiHashMap<int, int> m_NextGridPositionHashMap;

    protected override void OnStopRunning()
    {
        if (m_NextGridPositionHashMap.IsCreated)
            m_NextGridPositionHashMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (m_NextGridPositionHashMap.IsCreated)
            m_NextGridPositionHashMap.Dispose();

        var unitCount = query.CalculateEntityCount();
        m_NextGridPositionHashMap = new NativeMultiHashMap<int, int>(unitCount, Allocator.TempJob);

        var hashMap = m_NextGridPositionHashMap;
        var parallelWriter = m_NextGridPositionHashMap.AsParallelWriter();

        var hashNextGridPositionsJobHandle = Entities
            .WithName("HashNextGridPositions")
            .WithStoreEntityQueryInField(ref query)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in NextGridPosition nextGridPosition) =>
                {
                    var hash = (int)math.hash(nextGridPosition.Value);
                    parallelWriter.Add(hash, entityInQueryIndex);
                })
            .Schedule(inputDeps);

        var finalizeMovementJobHandle = Entities
            .WithName("FinalizeMovement")
            .WithReadOnly(hashMap)
            .WithBurst()
            .ForEach((ref NextGridPosition nextGridPosition, in GridPosition gridPosition) =>
                {
                    int hash = (int)math.hash(nextGridPosition.Value);
                    if (hashMap.TryGetFirstValue(hash, out int index, out var iter))
                    {
                        if (hashMap.TryGetNextValue(out _, ref iter))
                            nextGridPosition.Value = gridPosition.Value;
                    }
                })
            .Schedule(hashNextGridPositionsJobHandle);

        return finalizeMovementJobHandle;
    }
}
