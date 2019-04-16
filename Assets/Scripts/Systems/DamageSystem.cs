using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateAfter(typeof(MoveRandomlySystem))]
public class DamageSystem : JobComponentSystem
{
    private EntityQuery m_ZombieGroup;
    private EntityQuery m_HumanGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> zombieGridPositionsArray;
        public NativeArray<Damage> zombieDamageArray;
        public NativeMultiHashMap<int, int> zombieGridPositionsHashMap;
        public NativeArray<GridPosition> humanGridPositionsArray;
        public NativeArray<Damage> humanDamageArray;
        public NativeMultiHashMap<int, int> humanGridPositionsHashMap;
    }

    [BurstCompile]
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeMultiHashMap<int, int>.Concurrent hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(gridPositions[index].Value);
            hashMap.Add(hash, index);
        }
    }

    struct DamageJob : IJobForEachWithEntity<Health>
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        [ReadOnly] public NativeArray<Damage> damagingUnitsDamageArray;
        [ReadOnly] public NativeMultiHashMap<int, int> damagingUnitsHashMap;

        public void Execute(Entity entity, int index, ref Health health)
        {
            int3 myGridPosition = gridPositions[index].Value;
            int myHealth = health.Value;

            // Check all directions for damaging units
            for (int z = -1; z <= 1; z++)
            {
                for (int x = -1; x <= 1; x++)
                {
                    if (!(x == 0 && z == 0))
                    {
                        int damageKey = GridHash.Hash(new int3(myGridPosition.x + x, myGridPosition.y, myGridPosition.z + z));
                        if (damagingUnitsHashMap.TryGetFirstValue(damageKey, out int damageIndex, out _))
                            myHealth -= damagingUnitsDamageArray[damageIndex].Value;
                    }
                }
            }

            health = new Health { Value = myHealth };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieGridPositionsArray = m_ZombieGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var zombieDamageArray = m_ZombieGroup.ToComponentDataArray<Damage>(Allocator.TempJob);
        var zombieCount = zombieGridPositionsArray.Length;
        var zombieGridPositionsHashMap = new NativeMultiHashMap<int, int>(zombieCount, Allocator.TempJob);

        var humanGridPositionsArray = m_HumanGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var humanDamageArray = m_HumanGroup.ToComponentDataArray<Damage>(Allocator.TempJob);
        var humanCount = humanGridPositionsArray.Length;
        var humanGridPositionsHashMap = new NativeMultiHashMap<int, int>(humanCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            zombieGridPositionsArray = zombieGridPositionsArray,
            zombieDamageArray = zombieDamageArray,
            zombieGridPositionsHashMap = zombieGridPositionsHashMap,
            humanGridPositionsArray = humanGridPositionsArray,
            humanDamageArray = humanDamageArray,
            humanGridPositionsHashMap = humanGridPositionsHashMap,
        };
        if (m_PrevGridState.zombieGridPositionsArray.IsCreated)
            m_PrevGridState.zombieGridPositionsArray.Dispose();
        if (m_PrevGridState.zombieDamageArray.IsCreated)
            m_PrevGridState.zombieDamageArray.Dispose();
        if (m_PrevGridState.zombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.zombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.humanGridPositionsArray.IsCreated)
            m_PrevGridState.humanGridPositionsArray.Dispose();
        if (m_PrevGridState.humanDamageArray.IsCreated)
            m_PrevGridState.humanDamageArray.Dispose();
        if (m_PrevGridState.humanGridPositionsHashMap.IsCreated)
            m_PrevGridState.humanGridPositionsHashMap.Dispose();
        m_PrevGridState = nextGridState;

        var hashZombieGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = zombieGridPositionsArray,
            hashMap = zombieGridPositionsHashMap.ToConcurrent(),
        };
        var hashZombieGridPositionsJobHandle = hashZombieGridPositionsJob.Schedule(zombieCount, 64, inputDeps);

        var hashHumanGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = humanGridPositionsArray,
            hashMap = humanGridPositionsHashMap.ToConcurrent(),
        };
        var hashHumanGridPositionsJobHandle = hashHumanGridPositionsJob.Schedule(humanCount, 64, inputDeps);

        var hashGridPositionsBarrier = JobHandle.CombineDependencies(hashZombieGridPositionsJobHandle, hashHumanGridPositionsJobHandle);

        var damageZombiesJob = new DamageJob
        {
            gridPositions = zombieGridPositionsArray,
            damagingUnitsDamageArray = humanDamageArray,
            damagingUnitsHashMap = humanGridPositionsHashMap,
        };
        var damageZombiesJobHandle = damageZombiesJob.Schedule(m_ZombieGroup, hashGridPositionsBarrier);

        var damageHumansJob = new DamageJob
        {
            gridPositions = humanGridPositionsArray,
            damagingUnitsDamageArray = zombieDamageArray,
            damagingUnitsHashMap = zombieGridPositionsHashMap,
        };
        var damageHumansJobHandle = damageHumansJob.Schedule(m_HumanGroup, damageZombiesJobHandle);

        return JobHandle.CombineDependencies(damageZombiesJobHandle, damageHumansJobHandle);
    }

    protected override void OnCreateManager()
    {
        m_ZombieGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Zombie)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Damage)),
            typeof(Health)
        );
        m_HumanGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Damage)),
            typeof(Health)
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.zombieGridPositionsArray.IsCreated)
            m_PrevGridState.zombieGridPositionsArray.Dispose();
        if (m_PrevGridState.zombieDamageArray.IsCreated)
            m_PrevGridState.zombieDamageArray.Dispose();
        if (m_PrevGridState.zombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.zombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.humanGridPositionsArray.IsCreated)
            m_PrevGridState.humanGridPositionsArray.Dispose();
        if (m_PrevGridState.humanDamageArray.IsCreated)
            m_PrevGridState.humanDamageArray.Dispose();
        if (m_PrevGridState.humanGridPositionsHashMap.IsCreated)
            m_PrevGridState.humanGridPositionsHashMap.Dispose();
    }
}
