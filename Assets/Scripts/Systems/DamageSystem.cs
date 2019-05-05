using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateAfter(typeof(ResolveGridMovementSystem))]
public class DamageSystem : JobComponentSystem
{
    private EntityQuery m_ZombieGroup;
    private EntityQuery m_HumanGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> zombieGridPositionsArray;
        public NativeArray<Damage> zombieDamageArray;
        public NativeArray<TurnsUntilMove> zombieTurnsUntilMoveArray;
        public NativeMultiHashMap<int, int> zombieGridPositionsHashMap;
        public NativeArray<GridPosition> humanGridPositionsArray;
        public NativeArray<Damage> humanDamageArray;
        public NativeArray<TurnsUntilMove> humanTurnsUntilMoveArray;
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

    [BurstCompile]
    struct DamageJob : IJobForEachWithEntity<Health, HealthRange>
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        [ReadOnly] public NativeArray<Damage> damagingUnitsDamageArray;
        [ReadOnly] public NativeArray<TurnsUntilMove> turnsUntilMoveArray;
        [ReadOnly] public NativeMultiHashMap<int, int> damagingUnitsHashMap;
        public int health_75;
        public int health_50;
        public int health_25;

        public void Execute(Entity entity, int index, ref Health health, ref HealthRange healthRange)
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
                        { 
                            if (turnsUntilMoveArray[damageIndex].Value == 0)
                                myHealth -= damagingUnitsDamageArray[damageIndex].Value;
                        }
                    }
                }
            }

            if (health.Value >= health_75 && myHealth < health_75)
                healthRange = new HealthRange { Value = 75 };
            if (health.Value >= health_50 && myHealth < health_50)
                healthRange = new HealthRange { Value = 50 };
            if (health.Value >= health_25 && myHealth < health_25)
                healthRange = new HealthRange { Value = 25 };

            health = new Health { Value = myHealth };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieGridPositionsArray = m_ZombieGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var zombieDamageArray = m_ZombieGroup.ToComponentDataArray<Damage>(Allocator.TempJob);
        var zombieTurnsUntilMoveArray = m_ZombieGroup.ToComponentDataArray<TurnsUntilMove>(Allocator.TempJob);
        var zombieCount = zombieGridPositionsArray.Length;
        var zombieGridPositionsHashMap = new NativeMultiHashMap<int, int>(zombieCount, Allocator.TempJob);

        var humanGridPositionsArray = m_HumanGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var humanDamageArray = m_HumanGroup.ToComponentDataArray<Damage>(Allocator.TempJob);
        var humanTurnsUntilMoveArray = m_HumanGroup.ToComponentDataArray<TurnsUntilMove>(Allocator.TempJob);
        var humanCount = humanGridPositionsArray.Length;
        var humanGridPositionsHashMap = new NativeMultiHashMap<int, int>(humanCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            zombieGridPositionsArray = zombieGridPositionsArray,
            zombieDamageArray = zombieDamageArray,
            zombieTurnsUntilMoveArray = zombieTurnsUntilMoveArray,
            zombieGridPositionsHashMap = zombieGridPositionsHashMap,
            humanGridPositionsArray = humanGridPositionsArray,
            humanDamageArray = humanDamageArray,
            humanTurnsUntilMoveArray = humanTurnsUntilMoveArray,
            humanGridPositionsHashMap = humanGridPositionsHashMap,
        };
        if (m_PrevGridState.zombieGridPositionsArray.IsCreated)
            m_PrevGridState.zombieGridPositionsArray.Dispose();
        if (m_PrevGridState.zombieDamageArray.IsCreated)
            m_PrevGridState.zombieDamageArray.Dispose();
        if (m_PrevGridState.zombieTurnsUntilMoveArray.IsCreated)
            m_PrevGridState.zombieTurnsUntilMoveArray.Dispose();
        if (m_PrevGridState.zombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.zombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.humanGridPositionsArray.IsCreated)
            m_PrevGridState.humanGridPositionsArray.Dispose();
        if (m_PrevGridState.humanDamageArray.IsCreated)
            m_PrevGridState.humanDamageArray.Dispose();
        if (m_PrevGridState.humanTurnsUntilMoveArray.IsCreated)
            m_PrevGridState.humanTurnsUntilMoveArray.Dispose();
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
            turnsUntilMoveArray = humanTurnsUntilMoveArray,
            damagingUnitsHashMap = humanGridPositionsHashMap,
            health_75 = (int)(GameController.instance.zombieStartingHealth * 0.75),
            health_50 = (int)(GameController.instance.zombieStartingHealth * 0.50),
            health_25 = (int)(GameController.instance.zombieStartingHealth * 0.25),
        };
        var damageZombiesJobHandle = damageZombiesJob.Schedule(m_ZombieGroup, hashGridPositionsBarrier);

        var damageHumansJob = new DamageJob
        {
            gridPositions = humanGridPositionsArray,
            damagingUnitsDamageArray = zombieDamageArray,
            turnsUntilMoveArray = zombieTurnsUntilMoveArray,
            damagingUnitsHashMap = zombieGridPositionsHashMap,
            health_75 = (int)(GameController.instance.humanStartingHealth * 0.75),
            health_50 = (int)(GameController.instance.humanStartingHealth * 0.50),
            health_25 = (int)(GameController.instance.humanStartingHealth * 0.25),
        };
        var damageHumansJobHandle = damageHumansJob.Schedule(m_HumanGroup, damageZombiesJobHandle);

        return JobHandle.CombineDependencies(damageZombiesJobHandle, damageHumansJobHandle);
    }

    protected override void OnCreate()
    {
        m_ZombieGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Zombie)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Damage)),
            ComponentType.ReadOnly(typeof(TurnsUntilMove)),
            typeof(Health),
            typeof(HealthRange)
        );
        m_HumanGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Damage)),
            ComponentType.ReadOnly(typeof(TurnsUntilMove)),
            typeof(Health),
            typeof(HealthRange)
        );
    }

    protected override void OnStopRunning()
    {
        if (m_PrevGridState.zombieGridPositionsArray.IsCreated)
            m_PrevGridState.zombieGridPositionsArray.Dispose();
        if (m_PrevGridState.zombieDamageArray.IsCreated)
            m_PrevGridState.zombieDamageArray.Dispose();
        if (m_PrevGridState.zombieTurnsUntilMoveArray.IsCreated)
            m_PrevGridState.zombieTurnsUntilMoveArray.Dispose();
        if (m_PrevGridState.zombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.zombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.humanGridPositionsArray.IsCreated)
            m_PrevGridState.humanGridPositionsArray.Dispose();
        if (m_PrevGridState.humanDamageArray.IsCreated)
            m_PrevGridState.humanDamageArray.Dispose();
        if (m_PrevGridState.humanTurnsUntilMoveArray.IsCreated)
            m_PrevGridState.humanTurnsUntilMoveArray.Dispose();
        if (m_PrevGridState.humanGridPositionsHashMap.IsCreated)
            m_PrevGridState.humanGridPositionsHashMap.Dispose();
    }
}
