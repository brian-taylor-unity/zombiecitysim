using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateAfter(typeof(SpawnZombiesFromDeadHumansSystem))]
public class DamageToZombiesSystem : JobComponentSystem
{
    private EntityQuery query;
    private NativeMultiHashMap<int, int> m_DamageToZombiesHashMap;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var humanCount = query.CalculateEntityCount();

        if (humanCount == 0)
            return inputDeps;

        if (m_DamageToZombiesHashMap.IsCreated)
            m_DamageToZombiesHashMap.Dispose();

        m_DamageToZombiesHashMap = new NativeMultiHashMap<int, int>(humanCount * 8, Allocator.TempJob);

        var hashMap = m_DamageToZombiesHashMap;
        var parallelWriter = hashMap.AsParallelWriter();

        var calculateDamageFromHumansJobHandle = Entities
            .WithName("CalculateDamageFromHumans")
            .WithStoreEntityQueryInField(ref query)
            .WithAll<Human>()
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

        var dealDamageToZombiesJobHandle = Entities
            .WithName("DealDamageToZombies")
            .WithAll<Zombie>()
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
            .Schedule(calculateDamageFromHumansJobHandle);

        return dealDamageToZombiesJobHandle;
    }

    protected override void OnStopRunning()
    {
        if (m_DamageToZombiesHashMap.IsCreated)
            m_DamageToZombiesHashMap.Dispose();
    }
}
