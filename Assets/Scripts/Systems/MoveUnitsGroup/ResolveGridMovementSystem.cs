using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct HashNextGridPositionsJob : IJobEntity
{
    public NativeParallelMultiHashMap<int, int>.ParallelWriter ParallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in NextGridPosition nextGridPosition)
    {
        var hash = (int)math.hash(nextGridPosition.Value);
        ParallelWriter.Add(hash, entityIndexInQuery);
    }
}

[BurstCompile]
public partial struct FinalizeMovementJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, int> NextGridPositionHashMap;

    public void Execute(ref NextGridPosition nextGridPosition, in GridPosition gridPosition)
    {
        int hash = (int)math.hash(nextGridPosition.Value);
        if (NextGridPositionHashMap.TryGetFirstValue(hash, out _, out var iter))
        {
            if (NextGridPositionHashMap.TryGetNextValue(out _, ref iter))
                nextGridPosition.Value = gridPosition.Value;
        }
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateAfter(typeof(MoveTowardsHumansSystem))]
public partial struct ResolveGridMovementSystem : ISystem
{
    private EntityQuery _query;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
         _query = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
             .WithAllRW<NextGridPosition>()
             .WithAll<GridPosition, TurnActive>());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var unitCount = _query.CalculateEntityCount();
        var nextGridPositionHashMap = new NativeParallelMultiHashMap<int, int>(unitCount, Allocator.TempJob);

        state.Dependency = new HashNextGridPositionsJob { ParallelWriter = nextGridPositionHashMap.AsParallelWriter() }.ScheduleParallel(_query, state.Dependency);
        state.Dependency = new FinalizeMovementJob { NextGridPositionHashMap = nextGridPositionHashMap }.ScheduleParallel(_query, state.Dependency);
        nextGridPositionHashMap.Dispose(state.Dependency);
    }
}
