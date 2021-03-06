﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(DamageGroup))]
public class KillAndSpawnSystem : SystemBase
{
    private BeginSimulationEntityCommandBufferSystem m_EntityCommandBufferSystemBegin;
    private EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystemEnd;
    private NativeArray<UnitSpawner_Data> m_UnitSpawnerArray;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystemBegin = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
        m_EntityCommandBufferSystemEnd = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnStartRunning()
    {
        m_UnitSpawnerArray = EntityManager.CreateEntityQuery(typeof(UnitSpawner_Data)).ToComponentDataArray<UnitSpawner_Data>(Allocator.Persistent);
    }

    protected override void OnStopRunning()
    {
        m_UnitSpawnerArray.Dispose();
    }

    protected override void OnUpdate()
    {
        var commandBufferBegin = m_EntityCommandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter();
        var commandBufferEnd = m_EntityCommandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter();
        var unitSpawner = m_UnitSpawnerArray[0];
        var unitHealth = GameController.instance.zombieStartingHealth;
        var unitDamage = GameController.instance.zombieDamage;
        var unitTurnsUntilActive = GameController.instance.zombieTurnDelay;

        var killJob = Entities
            .WithName("KillUnits")
            .WithChangeFilter<Health>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, Entity entity, in Health health) =>
            {
                if (health.Value <= 0)
                {
                    commandBufferBegin.DestroyEntity(entityInQueryIndex, entity);
                    return;
                }
            })
            .ScheduleParallel(Dependency);
        m_EntityCommandBufferSystemBegin.AddJobHandleForProducer(killJob);

        var spawnJob = Entities
            .WithName("SpawnZombies")
            .WithAll<Human>()
            .WithChangeFilter<Health>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, Entity entity, in Health health, in GridPosition gridPosition) =>
                {
                    if (health.Value <= 0)
                    {
                        ZombieCreator.CreateZombie(
                            commandBufferEnd,
                            entityInQueryIndex,
                            unitSpawner.ZombieUnit_Prefab,
                            gridPosition.Value,
                            unitHealth,
                            unitDamage,
                            unitTurnsUntilActive,
                            entityInQueryIndex == 0 ? 1 : (uint)entityInQueryIndex
                        );
                    }
                })
            .ScheduleParallel(Dependency);
        m_EntityCommandBufferSystemEnd.AddJobHandleForProducer(spawnJob);

        Dependency = JobHandle.CombineDependencies(spawnJob, killJob);
    }
}
