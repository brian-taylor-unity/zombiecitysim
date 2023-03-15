using Unity.Entities;
using Unity.Jobs;

[UpdateInGroup(typeof(DamageGroup))]
public partial class KillAndSpawnSystem : SystemBase
{
    private BeginSimulationEntityCommandBufferSystem m_EntityCommandBufferSystemBegin;
    private EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystemEnd;

    protected override void OnCreate()
    {
        RequireForUpdate<TileUnitSpawner_Data>();

        m_EntityCommandBufferSystemBegin = World.GetOrCreateSystemManaged<BeginSimulationEntityCommandBufferSystem>();
        m_EntityCommandBufferSystemEnd = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var unitSpawner = SystemAPI.GetSingleton<TileUnitSpawner_Data>();

        var commandBufferBegin = m_EntityCommandBufferSystemBegin.CreateCommandBuffer().AsParallelWriter();
        var commandBufferEnd = m_EntityCommandBufferSystemEnd.CreateCommandBuffer().AsParallelWriter();
        var unitHealth = GameController.Instance.zombieStartingHealth;
        var unitDamage = GameController.Instance.zombieDamage;
        var unitVisionDistance = GameController.Instance.zombieVisionDistance;
        var unitHearingDistance = GameController.Instance.zombieHearingDistance;
        var unitTurnsUntilActive = GameController.Instance.zombieTurnDelay;

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
                            unitVisionDistance,
                            unitHearingDistance,
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
