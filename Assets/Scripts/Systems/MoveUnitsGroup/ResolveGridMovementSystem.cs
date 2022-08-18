using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateAfter(typeof(MoveTowardsTargetSystem))]
public partial class ResolveGridMovementSystem : SystemBase
{
    private EntityQuery query;

    protected override void OnUpdate()
    {
        var unitCount = query.CalculateEntityCount();
        var nextGridPositionHashMap = new NativeParallelMultiHashMap<int, int>(unitCount, Allocator.TempJob);
        var parallelWriter = nextGridPositionHashMap.AsParallelWriter();

        Entities
            .WithName("HashNextGridPositions")
            .WithStoreEntityQueryInField(ref query)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in NextGridPosition nextGridPosition) =>
                {
                    var hash = (int)math.hash(nextGridPosition.Value);
                    parallelWriter.Add(hash, entityInQueryIndex);
                })
            .ScheduleParallel();

        Entities
            .WithName("FinalizeMovement")
            .WithReadOnly(nextGridPositionHashMap)
            .WithDisposeOnCompletion(nextGridPositionHashMap)
            .WithBurst()
            .ForEach((ref NextGridPosition nextGridPosition, in GridPosition gridPosition) =>
                {
                    int hash = (int)math.hash(nextGridPosition.Value);
                    if (nextGridPositionHashMap.TryGetFirstValue(hash, out _, out var iter))
                    {
                        if (nextGridPositionHashMap.TryGetNextValue(out _, ref iter))
                            nextGridPosition.Value = gridPosition.Value;
                    }
                })
            .ScheduleParallel();
    }
}
