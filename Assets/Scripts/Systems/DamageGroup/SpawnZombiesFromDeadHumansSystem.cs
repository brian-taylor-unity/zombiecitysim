using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateAfter(typeof(DamageToHumansSystem))]
public class SpawnZombiesFromDeadHumansSystem : JobComponentSystem
{
    private EntityQuery m_HumansGroup;
    private EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    [BurstCompile]
    struct SpawnJob : IJobForEachWithEntity<UnitSpawner_Data>
    {
        [DeallocateOnJobCompletion] [ReadOnly] public NativeArray<int3> unitPositionsArray;
        [ReadOnly] public int unitHealth;
        [ReadOnly] public int unitDamage;
        [ReadOnly] public int unitTurnsUntilActive;

        public EntityCommandBuffer.Concurrent CommandBuffer;

        public void Execute(Entity entity, int index, [ReadOnly] ref UnitSpawner_Data unitSpawner)
        {
            for (int i = 0; i < unitPositionsArray.Length; i++)
            {
                Entity instance;
                instance = CommandBuffer.Instantiate(index, unitSpawner.ZombieUnit_Prefab);
                CommandBuffer.SetComponent(index, instance, new Translation { Value = unitPositionsArray[i] });
                CommandBuffer.AddComponent(index, instance, new GridPosition { Value = new int3(unitPositionsArray[i]) });
                CommandBuffer.AddComponent(index, instance, new NextGridPosition { Value = new int3(unitPositionsArray[i]) });
                CommandBuffer.AddComponent(index, instance, new Health { Value = unitHealth });
                CommandBuffer.AddComponent(index, instance, new HealthRange { Value = 100 });
                CommandBuffer.AddComponent(index, instance, new Damage { Value = unitDamage });
                CommandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = unitTurnsUntilActive });
                CommandBuffer.AddComponent(index, instance, new Zombie());
                CommandBuffer.AddComponent(index, instance, new DynamicCollidable());
                CommandBuffer.AddComponent(index, instance, new MoveTowardsTarget());
            }
        }

    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var gridPositionArray = m_HumansGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var healthArray = m_HumansGroup.ToComponentDataArray<Health>(Allocator.TempJob);

        var unitPositions = new List<int3>();

        for (int i = 0; i < healthArray.Length; i++)
        {
            if (healthArray[i].Value <= 0)
            {
                unitPositions.Add(gridPositionArray[i].Value);
            }
        }

        if (unitPositions.Count > 0)
        {
            var entity = EntityManager.CreateEntity();
            EntityManager.AddComponent(entity, typeof(UnitSpawner_Authoring));

            var spawnJob = new SpawnJob
            {
                unitPositionsArray = new NativeArray<int3>(unitPositions.ToArray(), Allocator.TempJob),
                unitHealth = GameController.instance.zombieStartingHealth,
                unitDamage = GameController.instance.zombieDamage,
                unitTurnsUntilActive = GameController.instance.zombieTurnDelay,
                CommandBuffer = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
            }.Schedule(this, inputDeps);

            m_EntityCommandBufferSystem.AddJobHandleForProducer(spawnJob);

            gridPositionArray.Dispose();
            healthArray.Dispose();

            return spawnJob;
        }

        gridPositionArray.Dispose();
        healthArray.Dispose();

        return inputDeps;
    }

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();

        m_HumansGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Health))
        );
    }
}
