using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
public class DamageToHumansSystem : JobComponentSystem
{
    private EntityQuery query;

    private NativeMultiHashMap<int, int> m_DamageToHumansHashMap;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieCount = query.CalculateEntityCount();

        if (zombieCount == 0)
            return inputDeps;

        if (m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap.Dispose();

        m_DamageToHumansHashMap = new NativeMultiHashMap<int, int>(zombieCount * 8, Allocator.TempJob);

        var hashMap = m_DamageToHumansHashMap;
        var parallelWriter = m_DamageToHumansHashMap.AsParallelWriter();

        var calculateDamageFromZombiesJobHandle = Entities
            .WithName("CalculateDamageFromZombies")
            .WithStoreEntityQueryInField(ref query)
            .WithAll<Zombie>()
            .WithChangeFilter<TurnsUntilActive>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, in TurnsUntilActive turnsUntilActive, in GridPosition gridPosition, in Damage damage) =>
                {
                    if (turnsUntilActive.Value != 0)
                        return;

                    for (int z = -1; z <= 1; z++)
                    {
                        for (int x = -1; x <= 1; x++)
                        {
                            if (!(x == 0 && z == 0))
                            {
                                int damageKey = (int)math.hash(new int3(gridPosition.Value.x + x, gridPosition.Value.y, gridPosition.Value.z + z));
                                parallelWriter.Add(damageKey, damage.Value);
                            }
                        }
                    }
                })
            .Schedule(inputDeps);

        var dealDamageToHumansJobHandle = Entities
            .WithName("DealDamageToHumans")
            .WithAll<Human>()
            .WithReadOnly(hashMap)
            .WithBurst()
            .ForEach((ref Health health, in GridPosition gridPosition) =>
                {
                    int myHealth = health.Value;

                    int gridPositionHash = (int)math.hash(new int3(gridPosition.Value));
                    if (hashMap.TryGetFirstValue(gridPositionHash, out var damage, out var it))
                    {
                        myHealth -= damage;

                        while (hashMap.TryGetNextValue(out damage, ref it))
                        {
                            myHealth -= damage;
                        }
                    }

                    health = new Health { Value = myHealth };
                })
            .Schedule(calculateDamageFromZombiesJobHandle);

        return dealDamageToHumansJobHandle;
    }

    protected override void OnStopRunning()
    {
        if (m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap.Dispose();
    }
}
