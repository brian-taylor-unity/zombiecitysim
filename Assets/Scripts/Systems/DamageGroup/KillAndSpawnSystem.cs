using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

[UpdateInGroup(typeof(DamageGroup))]
public class KillAndSpawnSystem : JobComponentSystem
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

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var commandBufferBegin = m_EntityCommandBufferSystemBegin.CreateCommandBuffer().ToConcurrent();
        var commandBufferEnd = m_EntityCommandBufferSystemEnd.CreateCommandBuffer().ToConcurrent();
        var unitSpawner = m_UnitSpawnerArray[0];
        var unitHealth = GameController.instance.zombieStartingHealth;
        var unitDamage = GameController.instance.zombieDamage;
        var unitTurnsUntilActive = GameController.instance.zombieTurnDelay;

        var spawnJob = Entities
            .WithName("SpawnZombies")
            .WithAll<Human>()
            .WithChangeFilter<Health>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, Entity entity, in Health health, in GridPosition gridPosition) =>
                {
                    if (health.Value <= 0)
                    {
                        var instance = commandBufferEnd.Instantiate(entityInQueryIndex, unitSpawner.ZombieUnit_Prefab);
                        commandBufferEnd.SetComponent(entityInQueryIndex, instance, new Translation { Value = gridPosition.Value });
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new GridPosition { Value = gridPosition.Value });
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new NextGridPosition { Value = gridPosition.Value });
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new Health { Value = unitHealth });
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new Damage { Value = unitDamage });
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new TurnsUntilActive { Value = unitTurnsUntilActive });
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new Zombie());
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new DynamicCollidable());
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new MoveTowardsTarget());
                        commandBufferEnd.AddComponent(entityInQueryIndex, instance, new MoveEscapeTarget());
                    }
                })
            .Schedule(inputDeps);
        m_EntityCommandBufferSystemEnd.AddJobHandleForProducer(spawnJob);

        var killJob = Entities
            .WithName("KillUnits")
            .WithChangeFilter<Health>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, Entity entity, in Health health) =>
                {
                    if (health.Value <= 0)
                        commandBufferBegin.DestroyEntity(entityInQueryIndex, entity);
                })
            .Schedule(inputDeps);
        m_EntityCommandBufferSystemBegin.AddJobHandleForProducer(killJob);

        return JobHandle.CombineDependencies(spawnJob, killJob);
    }
}
