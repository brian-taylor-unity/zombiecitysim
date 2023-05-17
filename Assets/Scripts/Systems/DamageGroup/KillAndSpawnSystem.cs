using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct KillUnitsJob : IJobEntity
{
    [NativeDisableParallelForRestriction]
    public ComponentLookup<Dead> DeadLookup;

    public void Execute(Entity entity, [ReadOnly] in Health health)
    {
        DeadLookup.SetComponentEnabled(entity, health.Value <= 0);
    }
}

[BurstCompile]
public partial struct SpawnZombiesJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;
    public Entity ZombiePrefab;
    public int UnitHealth;
    public int UnitDamage;
    public int UnitTurnsUntilActive;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, [ReadOnly] in GridPosition gridPosition)
    {
        ZombieCreator.CreateZombie(
            ref Ecb,
            entityIndexInQuery,
            ZombiePrefab,
            gridPosition.Value,
            UnitHealth,
            UnitDamage,
            UnitTurnsUntilActive,
            entityIndexInQuery == 0 ? 1 : (uint)entityIndexInQuery
        );
    }
}

[UpdateInGroup(typeof(DamageGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct KillAndSpawnSystem : ISystem
{
    private ComponentLookup<Dead> _deadLookup;
    private EntityQuery _deadQuery;
    private EntityQuery _unitQuery;
    private EntityQuery _humanQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _deadLookup = state.GetComponentLookup<Dead>();
        _deadQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAllRW<Dead>());
        _unitQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Health>());
        _unitQuery.SetChangedVersionFilter(ComponentType.ReadOnly<Health>());
        _humanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Human, Dead, GridPosition>());

        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireForUpdate<TileUnitSpawner_Data>();
        state.RequireForUpdate(_unitQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        _deadLookup.Update(ref state);
        new KillUnitsJob
        {
            DeadLookup = _deadLookup
        }.ScheduleParallel(_unitQuery, state.Dependency).Complete();

        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();
        var unitSpawner = SystemAPI.GetSingleton<TileUnitSpawner_Data>();

        state.Dependency = new SpawnZombiesJob
        {
            Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            ZombiePrefab = unitSpawner.ZombieUnit_Prefab,
            UnitHealth = gameControllerComponent.zombieStartingHealth,
            UnitDamage = gameControllerComponent.zombieDamage,
            UnitTurnsUntilActive = gameControllerComponent.zombieTurnDelay
        }.ScheduleParallel(_humanQuery, state.Dependency);

        state.EntityManager.DestroyEntity(_deadQuery);
    }
}
