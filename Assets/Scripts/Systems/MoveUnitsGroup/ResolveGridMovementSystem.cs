using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[BurstCompile]
public partial struct HashNextGridPositionsJob : IJobEntity
{
    public NativeParallelMultiHashMap<int, int>.ParallelWriter parallelWriter;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in NextGridPosition nextGridPosition)
    {
        var hash = (int)math.hash(nextGridPosition.Value);
        parallelWriter.Add(hash, entityIndexInQuery);
    }
}

[BurstCompile]
public partial struct FinalizeMovementJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, int> nextGridPositionHashMap;

    public void Execute(ref NextGridPosition nextGridPosition, in GridPosition gridPosition)
    {
        int hash = (int)math.hash(nextGridPosition.Value);
        if (nextGridPositionHashMap.TryGetFirstValue(hash, out _, out var iter))
        {
            if (nextGridPositionHashMap.TryGetNextValue(out _, ref iter))
                nextGridPosition.Value = gridPosition.Value;
        }
    }
}

[BurstCompile]
public partial struct DisableTurnActiveJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public ComponentLookup<TurnActive> TurnActiveFromEntity;

    public void Execute(Entity entity)
    {
        TurnActiveFromEntity.SetComponentEnabled(entity, false);
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateAfter(typeof(MoveTowardsHumansSystem))]
public partial struct ResolveGridMovementSystem : ISystem
{
    private EntityQuery _query;
    private ComponentLookup<TurnActive> _turnActiveFromEntity;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
         _query = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
             .WithAllRW<NextGridPosition>()
             .WithAll<GridPosition, TurnActive>());

         _turnActiveFromEntity = state.GetComponentLookup<TurnActive>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var unitCount = _query.CalculateEntityCount();
        var nextGridPositionHashMap = new NativeParallelMultiHashMap<int, int>(unitCount, Allocator.TempJob);

        state.Dependency = new HashNextGridPositionsJob { parallelWriter = nextGridPositionHashMap.AsParallelWriter() }.ScheduleParallel(_query, state.Dependency);
        state.Dependency = new FinalizeMovementJob { nextGridPositionHashMap = nextGridPositionHashMap }.ScheduleParallel(_query, state.Dependency);
        nextGridPositionHashMap.Dispose(state.Dependency);

        _turnActiveFromEntity.Update(ref state);
        state.Dependency = new DisableTurnActiveJob { TurnActiveFromEntity = _turnActiveFromEntity }.ScheduleParallel(_query, state.Dependency);
    }
}
