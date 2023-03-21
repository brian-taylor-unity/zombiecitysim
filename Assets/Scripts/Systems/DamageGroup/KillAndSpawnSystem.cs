using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct KillUnitsJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, Entity entity, in Health health)
    {
        if (health.Value <= 0)
        {
            Ecb.DestroyEntity(entityIndexInQuery, entity);
        }
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

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, in Health health, in GridPosition gridPosition)
    {
        if (health.Value <= 0)
        {
            ZombieCreator.CreateZombie(
                Ecb,
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
}

[UpdateInGroup(typeof(DamageGroup))]
public partial struct KillAndSpawnSystem : ISystem
{
    private EntityQuery _unitQuery;
    private EntityQuery _humanQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireForUpdate<TileUnitSpawner_Data>();

        _unitQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Health>());
        _unitQuery.SetChangedVersionFilter(ComponentType.ReadOnly<Health>());
        _humanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Human, Health, GridPosition>());
        _humanQuery.SetChangedVersionFilter(ComponentType.ReadOnly<Health>());
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        new KillUnitsJob
        {
            Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        }.ScheduleParallel(_unitQuery);

        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();
        var unitSpawner = SystemAPI.GetSingleton<TileUnitSpawner_Data>();

        new SpawnZombiesJob
        {
            Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            ZombiePrefab = unitSpawner.ZombieUnit_Prefab,
            UnitHealth = gameControllerComponent.zombieStartingHealth,
            UnitDamage = gameControllerComponent.zombieDamage,
            UnitTurnsUntilActive = gameControllerComponent.zombieTurnDelay
        }.ScheduleParallel(_humanQuery);
    }
}
