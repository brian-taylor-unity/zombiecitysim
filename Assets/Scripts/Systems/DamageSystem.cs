﻿using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateAfter(typeof(MoveRandomlySystem))]
public class DamageSystem : JobComponentSystem
{
    private ComponentGroup m_ZombieGroup;
    private ComponentGroup m_HumanGroup;

    [BurstCompile]
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> gridPositions;
        public NativeMultiHashMap<int, int>.Concurrent hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(gridPositions[index].Value);
            hashMap.Add(hash, index);
        }
    }

    struct DamageJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> gridPositions;
        [ReadOnly] public ComponentDataArray<Damage> damagingUnitsDamageArray;
        [ReadOnly] public NativeMultiHashMap<int, int> damagingUnitsHashMap;
        public ComponentDataArray<Health> healthArray;

        public void Execute(int index)
        {
            int3 myGridPosition = gridPositions[index].Value;
            int myHealth = healthArray[index].Value;

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

            healthArray[index] = new Health { Value = myHealth };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieGridPositionComponents = m_ZombieGroup.GetComponentDataArray<GridPosition>();
        var zombieGridPositionsHashMap = new NativeMultiHashMap<int, int>(zombieGridPositionComponents.Length, Allocator.TempJob);
        var zombieHealthComponents = m_ZombieGroup.GetComponentDataArray<Health>();
        var zombieDamageComponents = m_ZombieGroup.GetComponentDataArray<Damage>();

        var humanGridPositionComponents = m_HumanGroup.GetComponentDataArray<GridPosition>();
        var humanGridPositionsHashMap = new NativeMultiHashMap<int, int>(humanGridPositionComponents.Length, Allocator.TempJob);
        var humanHealthComponents = m_HumanGroup.GetComponentDataArray<Health>();
        var humanDamageComponents = m_HumanGroup.GetComponentDataArray<Damage>();

        var hashZombieGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = zombieGridPositionComponents,
            hashMap = zombieGridPositionsHashMap.ToConcurrent(),
        };
        var hashZombieGridPositionsJobHandle = hashZombieGridPositionsJob.Schedule(zombieDamageComponents.Length, 64, inputDeps);

        var hashHumanGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = humanGridPositionComponents,
            hashMap = humanGridPositionsHashMap.ToConcurrent(),
        };
        var hashHumanGridPositionsJobHandle = hashHumanGridPositionsJob.Schedule(humanDamageComponents.Length, 64, inputDeps);

        var hashGridPositionsBarrier = JobHandle.CombineDependencies(hashZombieGridPositionsJobHandle, hashHumanGridPositionsJobHandle);

        var damageZombiesJob = new DamageJob
        {
            gridPositions = zombieGridPositionComponents,
            damagingUnitsDamageArray = humanDamageComponents,
            damagingUnitsHashMap = humanGridPositionsHashMap,
            healthArray = zombieHealthComponents,
        };
        var damageZombiesJobHandle = damageZombiesJob.Schedule(zombieDamageComponents.Length, 64, hashGridPositionsBarrier);

        var damageHumansJob = new DamageJob
        {
            gridPositions = humanGridPositionComponents,
            damagingUnitsDamageArray = zombieDamageComponents,
            damagingUnitsHashMap = zombieGridPositionsHashMap,
            healthArray = humanHealthComponents,
        };
        var damageHumansJobHandle = damageHumansJob.Schedule(humanDamageComponents.Length, 64, damageZombiesJobHandle);

        damageZombiesJobHandle.Complete();
        damageHumansJobHandle.Complete();
        zombieGridPositionsHashMap.Dispose();
        humanGridPositionsHashMap.Dispose();

        return damageHumansJobHandle;
    }

    protected override void OnCreateManager()
    {
        m_ZombieGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Zombie)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Damage)),
            typeof(Health)
        );
        m_HumanGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Damage)),
            typeof(Health)
        );
    }
}
