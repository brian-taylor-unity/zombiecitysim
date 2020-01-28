using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateBefore(typeof(RemoveDeadUnitsSystem))]
public class DamageToHumansSystem : JobComponentSystem
{
    private EntityQuery m_HumanGroup;
    private EntityQuery m_ZombieGroup;

    private NativeMultiHashMap<int, int> m_DamageToHumansHashMap;


    [BurstCompile]
    struct CalculateDamageJob : IJobForEachWithEntity<GridPosition, Damage, TurnsUntilActive>
    {
        public NativeMultiHashMap<int, int>.ParallelWriter damageHashMap;

        public void Execute(Entity entity, int index, [ReadOnly] ref GridPosition gridPosition, [ReadOnly] ref Damage damage, [ReadOnly] ref TurnsUntilActive turnsUntilActive)
        {
            if (turnsUntilActive.Value != 0)
                return;

            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (!(x == 0 && z == 0))
                    {
                        int damageKey = GridHash.Hash(new int3(gridPosition.Value.x + x, gridPosition.Value.y, gridPosition.Value.z + z));
                        damageHashMap.Add(damageKey, damage.Value);
                    }
                }
            }
        }
    }

    [BurstCompile]
    struct DealDamageToHumansJob : IJobForEachWithEntity<Human, GridPosition, Health>
    {
        [ReadOnly] public NativeMultiHashMap<int, int> damageHashMap;

        public void Execute(Entity entity, int index, [ReadOnly] ref Human human, [ReadOnly] ref GridPosition gridPosition, ref Health health)
        {
            int myHealth = health.Value;

            int gridPositionHash = GridHash.Hash(new int3(gridPosition.Value));
            if (damageHashMap.TryGetFirstValue(gridPositionHash, out var damage, out var it))
            { 
                myHealth -= damage;

                while (damageHashMap.TryGetNextValue(out damage, ref it))
                {
                    myHealth -= damage;
                }
            }

            health = new Health { Value = myHealth };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap = new NativeMultiHashMap<int, int>(GameController.instance.numTilesX * GameController.instance.numTilesY, Allocator.Persistent);

        m_DamageToHumansHashMap.Clear();

        var calculateDamageFromZombiesJob = new CalculateDamageJob
        {
            damageHashMap = m_DamageToHumansHashMap.AsParallelWriter()
        };
        var calculateDamageFromZombiesJobHandle = calculateDamageFromZombiesJob.Schedule(m_ZombieGroup, inputDeps);

        var dealDamageToHumansJob = new DealDamageToHumansJob
        {
            damageHashMap = m_DamageToHumansHashMap
        };
        var dealDamageToHumansJobHandle = dealDamageToHumansJob.Schedule(m_HumanGroup, calculateDamageFromZombiesJobHandle);

        return dealDamageToHumansJobHandle;
    }

    protected override void OnCreate()
    {
        m_HumanGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            typeof(Health)
        );
        m_ZombieGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Zombie)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Damage)),
            ComponentType.ReadOnly(typeof(TurnsUntilActive))
        );
    }

    protected override void OnDestroy()
    {
        if (m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap.Dispose();
    }
}
