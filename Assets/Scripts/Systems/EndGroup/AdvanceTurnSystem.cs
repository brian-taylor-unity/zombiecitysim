using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct SetTurnActiveJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public ComponentLookup<TurnActive> TurnActiveFromEntity;

    public void Execute(Entity entity, [ReadOnly] in TurnsUntilActive turnsUntilActive)
    {
        TurnActiveFromEntity.SetComponentEnabled(entity, turnsUntilActive.Value == 1);
    }
}

[BurstCompile]
public partial struct AdvanceUnitsTurnJob : IJobEntity
{
    public float TurnDelayTime;
    public int UnitTurnDelay;

    public void Execute(ref TurnsUntilActive turnsUntilActive, ref CharacterColor characterColor)
    {
        characterColor.Value.w = math.select(1.0f, math.select(0.85f, 1.0f, turnsUntilActive.Value == 2), TurnDelayTime >= 0.2);
        turnsUntilActive.Value = math.select(turnsUntilActive.Value - 1, UnitTurnDelay, turnsUntilActive.Value == 1);
    }
}

[BurstCompile]
public partial struct AdvanceAudiblesAgeJob : IJobEntity
{
    public int AudibleDecayTime;
    public EntityCommandBuffer.ParallelWriter ecb;

    public void Execute(Entity entity, [EntityIndexInQuery] int entityIndexInQuery, ref Audible audible)
    {
        audible.Age += 1;
        if (audible.Age > AudibleDecayTime)
            ecb.DestroyEntity(entityIndexInQuery, entity);
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

[UpdateInGroup(typeof(EndGroup))]
public partial struct AdvanceTurnSystem : ISystem
{
    private ComponentLookup<TurnActive> _turnActiveFromEntity;
    private EntityQuery _turnActiveQuery;
    private EntityQuery _humanQuery;
    private EntityQuery _zombieQuery;
    private double _LastTime;

    public void OnCreate(ref SystemState state)
    {
        _turnActiveFromEntity = state.GetComponentLookup<TurnActive>();
        _turnActiveQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAllRW<TurnActive>());
        _humanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Human>()
            .WithAllRW<TurnsUntilActive, CharacterColor>());
        _zombieQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Zombie>()
            .WithAllRW<TurnsUntilActive, CharacterColor>());

        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireAnyForUpdate(_humanQuery, _zombieQuery, state.GetEntityQuery(ComponentType.ReadWrite<Audible>()));

        _LastTime = SystemAPI.Time.ElapsedTime;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _turnActiveFromEntity.Update(ref state);
        state.Dependency = new DisableTurnActiveJob
        {
            TurnActiveFromEntity = _turnActiveFromEntity
        }.ScheduleParallel(_turnActiveQuery, state.Dependency);

        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        var turnDelayTime = gameControllerComponent.turnDelayTime;
        var now = SystemAPI.Time.ElapsedTime;
        if (now - _LastTime > turnDelayTime)
        {
            _LastTime = now;

            var advanceAudiblesAgeJobHandle = new AdvanceAudiblesAgeJob
            {
                AudibleDecayTime = gameControllerComponent.audibleDecayTime,
                ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                    .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
            }.ScheduleParallel(state.Dependency);

            state.Dependency = new AdvanceUnitsTurnJob
            {
                TurnDelayTime = turnDelayTime,
                UnitTurnDelay = gameControllerComponent.humanTurnDelay
            }.ScheduleParallel(_humanQuery, state.Dependency);

            state.Dependency = new AdvanceUnitsTurnJob
            {
                TurnDelayTime = turnDelayTime,
                UnitTurnDelay = gameControllerComponent.zombieTurnDelay
            }.ScheduleParallel(_zombieQuery, state.Dependency);

            _turnActiveFromEntity.Update(ref state);
            state.Dependency = new SetTurnActiveJob
            {
                TurnActiveFromEntity = _turnActiveFromEntity
            }.ScheduleParallel(state.Dependency);

            state.Dependency = JobHandle.CombineDependencies(advanceAudiblesAgeJobHandle, state.Dependency);
        }
    }
}
