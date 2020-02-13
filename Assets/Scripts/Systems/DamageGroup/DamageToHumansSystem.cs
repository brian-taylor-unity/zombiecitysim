using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
public class DamageToHumansSystem : JobComponentSystem
{
    private EntityQuery humansQuery;
    private EntityQuery zombiesQuery;
    private NativeHashMap<int, int> m_HumansHashMap;
    private NativeMultiHashMap<int, int> m_DamageToHumansHashMap;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var humanCount = humansQuery.CalculateEntityCount();
        var zombieCount = zombiesQuery.CalculateEntityCount();

        if (humanCount == 0 || zombieCount == 0)
            return inputDeps;

        if (m_HumansHashMap.IsCreated)
            m_HumansHashMap.Dispose();
        if (m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap.Dispose();

        m_HumansHashMap = new NativeHashMap<int, int>(humanCount, Allocator.TempJob);
        m_DamageToHumansHashMap = new NativeMultiHashMap<int, int>(zombieCount * 8, Allocator.TempJob);

        var humanHashMap = m_HumansHashMap;
        var humanHashMapParallelWriter = m_HumansHashMap.AsParallelWriter();

        var hashZombiesJobHandle = Entities
            .WithName("HashHumans")
            .WithStoreEntityQueryInField(ref humansQuery)
            .WithAll<Human>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
            {
                var hash = (int)math.hash(gridPosition.Value);
                humanHashMapParallelWriter.TryAdd(hash, entityInQueryIndex);
            })
            .Schedule(inputDeps);

        var damageHashMap = m_DamageToHumansHashMap;
        var damageHashMapParallelWriter = m_DamageToHumansHashMap.AsParallelWriter();

        var calculateDamageFromZombiesJobHandle = Entities
            .WithName("CalculateDamageFromZombies")
            .WithStoreEntityQueryInField(ref zombiesQuery)
            .WithAll<Zombie>()
            .WithChangeFilter<TurnsUntilActive>()
            .WithReadOnly(humanHashMap)
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
                                if (humanHashMap.TryGetValue(damageKey, out _))
                                    damageHashMapParallelWriter.Add(damageKey, damage.Value);
                            }
                        }
                    }
                })
            .Schedule(hashZombiesJobHandle);

        var dealDamageToHumansJobHandle = Entities
            .WithName("DealDamageToHumans")
            .WithAll<Human>()
            .WithReadOnly(damageHashMap)
            .WithBurst()
            .ForEach((ref Health health, ref HealthRange healthRange, in GridPosition gridPosition) =>
                {
                    int myHealth = health.Value;

                    int gridPositionHash = (int)math.hash(new int3(gridPosition.Value));
                    if (damageHashMap.TryGetFirstValue(gridPositionHash, out var damage, out var it))
                    {
                        myHealth -= damage;

                        while (damageHashMap.TryGetNextValue(out damage, ref it))
                        {
                            myHealth -= damage;
                        }

                        if (health.Value > 75 && myHealth <= 75)
                            healthRange.Value = 75;
                        if (health.Value > 50 && myHealth <= 50)
                            healthRange.Value = 50;
                        if (health.Value > 25 && myHealth <= 25)
                            healthRange.Value = 25;

                        health.Value = myHealth;
                    }
                })
            .Schedule(calculateDamageFromZombiesJobHandle);

        return dealDamageToHumansJobHandle;
    }

    protected override void OnStopRunning()
    {
        if (m_HumansHashMap.IsCreated)
            m_HumansHashMap.Dispose();
        if (m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap.Dispose();
    }
}
