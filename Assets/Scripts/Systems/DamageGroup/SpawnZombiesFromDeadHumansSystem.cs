using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateAfter(typeof(DamageToHumansSystem))]
public class SpawnZombiesFromDeadHumansSystem : JobComponentSystem
{
    private EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystem;
    private NativeArray<UnitSpawner_Data> m_UnitSpawnerArray;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
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
        var commandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var unitSpawner = m_UnitSpawnerArray[0];
        var unitHealth = GameController.instance.zombieStartingHealth;
        var unitDamage = GameController.instance.zombieDamage;
        var unitTurnsUntilActive = GameController.instance.zombieTurnDelay;

        var spawnJob = Entities
            .WithName("SpawnZombies")
            .WithAll<Human>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, Entity entity, in Health health, in GridPosition gridPosition) =>
                {
                    if (health.Value <= 0)
                    {
                        commandBuffer.DestroyEntity(entityInQueryIndex, entity);

                        var instance = commandBuffer.Instantiate(entityInQueryIndex, unitSpawner.ZombieUnit_Prefab);
                        commandBuffer.SetComponent(entityInQueryIndex, instance, new Translation { Value = gridPosition.Value });
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new GridPosition { Value = gridPosition.Value });
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new NextGridPosition { Value = gridPosition.Value });
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new Health { Value = unitHealth });
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new HealthRange { Value = 100 });
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new Damage { Value = unitDamage });
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new TurnsUntilActive { Value = unitTurnsUntilActive });
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new Zombie());
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new DynamicCollidable());
                        commandBuffer.AddComponent(entityInQueryIndex, instance, new MoveTowardsTarget());
                    }
                })
            .Schedule(inputDeps);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(spawnJob);

        var killJob = Entities
            .WithName("KillZombies")
            .WithAll<Zombie>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, Entity entity, in Health health) =>
                {
                    if (health.Value <= 0)
                        commandBuffer.DestroyEntity(entityInQueryIndex, entity);
                })
            .Schedule(spawnJob);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(killJob);

        return killJob;
    }
}
