using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

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

[BurstCompile]
public partial struct KillUnitsJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, Entity entity, [ReadOnly] in Dead dead)
    {
        Ecb.DestroyEntity(entityIndexInQuery, entity);
    }
}

[UpdateInGroup(typeof(DamageGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct KillAndSpawnSystem : ISystem
{
    private EntityQuery _humanQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _humanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Human, Dead, GridPosition>());

        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<EndSimulationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireForUpdate<TileUnitSpawner_Data>();
        state.RequireForUpdate(_humanQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();
        var unitSpawner = SystemAPI.GetSingleton<TileUnitSpawner_Data>();

        state.Dependency = new SpawnZombiesJob
        {
            Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),
            ZombiePrefab = unitSpawner.ZombieUnit_Prefab,
            UnitHealth = gameControllerComponent.zombieStartingHealth,
            UnitDamage = gameControllerComponent.zombieDamage,
            UnitTurnsUntilActive = gameControllerComponent.zombieTurnDelay
        }.ScheduleParallel(_humanQuery, state.Dependency);

        state.Dependency = new KillUnitsJob
        {
            Ecb = SystemAPI.GetSingleton<EndSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        }.ScheduleParallel(state.Dependency);
    }
}
