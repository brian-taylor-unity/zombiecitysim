using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashNextGridPositionsJob : IJobEntity
{
    public NativeParallelMultiHashMap<uint, int>.ParallelWriter ParallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, [ReadOnly] in DesiredNextGridPosition desiredNextGridPosition)
    {
        var hash = math.hash(desiredNextGridPosition.Value);
        ParallelWriter.Add(hash, entityIndexInQuery);
    }
}

[BurstCompile]
public partial struct FinalizeMovementJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<uint, int> NextGridPositionHashMap;

    public void Execute(ref DesiredNextGridPosition desiredNextGridPosition, [ReadOnly] in GridPosition gridPosition)
    {
        // Check for all units that wanted to move
        var hash = math.hash(desiredNextGridPosition.Value);
        if (!NextGridPositionHashMap.TryGetFirstValue(hash, out _, out var iter))
            return;

        // Don't allow movement if another unit has already claimed that grid space
        // (that unit is the first entry in the multi-hashmap)
        if (NextGridPositionHashMap.TryGetNextValue(out _, ref iter))
            desiredNextGridPosition.Value = gridPosition.Value;
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateAfter(typeof(MoveTowardsHumansSystem))]
[RequireMatchingQueriesForUpdate]
public partial struct ResolveGridMovementSystem : ISystem
{
    private EntityQuery _query;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
         _query = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
             .WithAllRW<DesiredNextGridPosition>()
             .WithAll<GridPosition, TurnActive>());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var unitCount = _query.CalculateEntityCount();
        var nextGridPositionHashMap = new NativeParallelMultiHashMap<uint, int>(unitCount, Allocator.TempJob);

        state.Dependency = new HashNextGridPositionsJob { ParallelWriter = nextGridPositionHashMap.AsParallelWriter() }.ScheduleParallel(_query, state.Dependency);
        state.Dependency = new FinalizeMovementJob { NextGridPositionHashMap = nextGridPositionHashMap }.ScheduleParallel(_query, state.Dependency);
        nextGridPositionHashMap.Dispose(state.Dependency);
    }
}
