using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct MarkDeadUnitsJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public ComponentLookup<Dead> DeadLookup;

    public void Execute(Entity entity, [ReadOnly] in Health health)
    {
        DeadLookup.SetComponentEnabled(entity, health.Value <= 0);
    }
}

[UpdateInGroup(typeof(DamageGroup))]
[UpdateBefore(typeof(KillAndSpawnSystem))]
[RequireMatchingQueriesForUpdate]
public partial struct MarkDeadUnitsSystem : ISystem
{
    private ComponentLookup<Dead> _deadLookup;
    private EntityQuery _unitQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _deadLookup = state.GetComponentLookup<Dead>();
        _unitQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Health>());
        _unitQuery.SetChangedVersionFilter(ComponentType.ReadOnly<Health>());

        state.RequireForUpdate<RunWorld>();
        state.RequireForUpdate(_unitQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _deadLookup.Update(ref state);
        state.Dependency = new MarkDeadUnitsJob
        {
            DeadLookup = _deadLookup
        }.ScheduleParallel(_unitQuery, state.Dependency);
    }
}