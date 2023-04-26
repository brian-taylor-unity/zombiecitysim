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

    public float TurnDelayTime;

    public void Execute(Entity entity, ref CharacterColor characterColor, [ReadOnly] in TurnsUntilActive turnsUntilActive)
    {
        TurnActiveFromEntity.SetComponentEnabled(entity, turnsUntilActive.Value == 1);
        characterColor.Value.w = math.select(1.0f, math.select(0.85f, 1.0f, turnsUntilActive.Value == 1), TurnDelayTime >= 0.2);
    }
}

[BurstCompile]
public partial struct AdvanceUnitsTurnJob : IJobEntity
{
    public int UnitTurnDelay;

    public void Execute(ref TurnsUntilActive turnsUntilActive)
    {
        turnsUntilActive.Value = math.select(turnsUntilActive.Value - 1, UnitTurnDelay, turnsUntilActive.Value == 1);
    }
}

[BurstCompile]
public partial struct AdvanceAudiblesAgeJob : IJobEntity
{
    public int AudibleDecayTime;
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, Entity entity, ref Audible audible)
    {
        audible.Age += 1;
        if (audible.Age > AudibleDecayTime)
            Ecb.DestroyEntity(entityIndexInQuery, entity);
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
[RequireMatchingQueriesForUpdate]
public partial struct AdvanceTurnSystem : ISystem
{
    private ComponentLookup<TurnActive> _turnActiveFromEntity;
    private EntityQuery _turnActiveQuery;
    private EntityQuery _humanQuery;
    private EntityQuery _zombieQuery;
    private double _lastTime;

    public void OnCreate(ref SystemState state)
    {
        _turnActiveFromEntity = state.GetComponentLookup<TurnActive>();
        _turnActiveQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAllRW<TurnActive>());
        _humanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Human>()
            .WithAllRW<TurnsUntilActive>());
        _zombieQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Zombie>()
            .WithAllRW<TurnsUntilActive>());

        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireAnyForUpdate(_humanQuery, _zombieQuery, state.GetEntityQuery(ComponentType.ReadWrite<Audible>()));

        _lastTime = SystemAPI.Time.ElapsedTime;
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();
        var turnDelayTime = gameControllerComponent.turnDelayTime;
        var now = SystemAPI.Time.ElapsedTime;

        // It only takes 1 frame for a unit to take an action, so we disable TurnActive every frame
        _turnActiveFromEntity.Update(ref state);
        state.Dependency = new DisableTurnActiveJob
        {
            TurnActiveFromEntity = _turnActiveFromEntity
        }.ScheduleParallel(_turnActiveQuery, state.Dependency);

        // Only progress with updating the turn values if enough time has elapsed
        if (!(now - _lastTime > turnDelayTime))
            return;

        _lastTime = now;

        var advanceAudiblesAgeJobHandle = new AdvanceAudiblesAgeJob
        {
            AudibleDecayTime = gameControllerComponent.audibleDecayTime,
            Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        }.ScheduleParallel(state.Dependency);

        state.Dependency = new AdvanceUnitsTurnJob { UnitTurnDelay = gameControllerComponent.humanTurnDelay }.ScheduleParallel(_humanQuery, state.Dependency);
        state.Dependency = new AdvanceUnitsTurnJob { UnitTurnDelay = gameControllerComponent.zombieTurnDelay }.ScheduleParallel(_zombieQuery, state.Dependency);

        _turnActiveFromEntity.Update(ref state);
        state.Dependency = new SetTurnActiveJob
        {
            TurnActiveFromEntity = _turnActiveFromEntity,
            TurnDelayTime = turnDelayTime
        }.ScheduleParallel(state.Dependency);

        state.Dependency = JobHandle.CombineDependencies(advanceAudiblesAgeJobHandle, state.Dependency);
    }
}
