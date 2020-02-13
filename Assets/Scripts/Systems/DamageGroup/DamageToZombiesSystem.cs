using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateAfter(typeof(SpawnZombiesFromDeadHumansSystem))]
public class DamageToZombiesSystem : JobComponentSystem
{
    private EntityQuery zombiesQuery;
    private EntityQuery humansQuery;
    private NativeHashMap<int, int> m_ZombiesHashMap;
    private NativeMultiHashMap<int, int> m_DamageToZombiesHashMap;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var zombieCount = zombiesQuery.CalculateEntityCount();
        var humanCount = humansQuery.CalculateEntityCount();

        if (zombieCount == 0 || humanCount == 0)
            return inputDeps;

        if (m_ZombiesHashMap.IsCreated)
            m_ZombiesHashMap.Dispose();
        if (m_DamageToZombiesHashMap.IsCreated)
            m_DamageToZombiesHashMap.Dispose();

        m_ZombiesHashMap = new NativeHashMap<int, int>(zombieCount, Allocator.TempJob);
        m_DamageToZombiesHashMap = new NativeMultiHashMap<int, int>(humanCount * 8, Allocator.TempJob);

        var zombieHashMap = m_ZombiesHashMap;
        var zombieHashMapParallelWriter = m_ZombiesHashMap.AsParallelWriter();

        var hashZombiesJobHandle = Entities
            .WithName("HashZombies")
            .WithStoreEntityQueryInField(ref zombiesQuery)
            .WithAll<Zombie>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    zombieHashMapParallelWriter.TryAdd(hash, entityInQueryIndex);
                })
            .Schedule(inputDeps);

        var damageHashMap = m_DamageToZombiesHashMap;
        var damageHashMapParallelWriter = damageHashMap.AsParallelWriter();

        var calculateDamageFromHumansJobHandle = Entities
            .WithName("CalculateDamageFromHumans")
            .WithStoreEntityQueryInField(ref humansQuery)
            .WithAll<Human>()
            .WithChangeFilter<TurnsUntilActive>()
            .WithReadOnly(zombieHashMap)
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
                                if (zombieHashMap.TryGetValue(damageKey, out _))
                                    damageHashMapParallelWriter.Add(damageKey, damage.Value);
                            }
                        }
                    }
                })
            .Schedule(hashZombiesJobHandle);

        var dealDamageToZombiesJobHandle = Entities
            .WithName("DealDamageToZombies")
            .WithAll<Zombie>()
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
            .Schedule(calculateDamageFromHumansJobHandle);

        return dealDamageToZombiesJobHandle;
    }

    protected override void OnStopRunning()
    {
        if (m_ZombiesHashMap.IsCreated)
            m_ZombiesHashMap.Dispose();
        if (m_DamageToZombiesHashMap.IsCreated)
            m_DamageToZombiesHashMap.Dispose();
    }
}
