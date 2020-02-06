using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateBefore(typeof(RemoveDeadUnitsSystem))]
public class DamageToHumansSystem : JobComponentSystem
{
    private NativeMultiHashMap<int, int> m_DamageToHumansHashMap;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        if (!m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap = new NativeMultiHashMap<int, int>(GameController.instance.numTilesX * GameController.instance.numTilesY, Allocator.Persistent);

        m_DamageToHumansHashMap.Clear();
        var parallelWriter = m_DamageToHumansHashMap.AsParallelWriter();

        var calculateDamageFromZombiesJobHandle = Entities
            .WithAll<Zombie>()
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

        var hashMap = m_DamageToHumansHashMap;
        var dealDamageToHumansJobHandle = Entities
            .WithAll<Human>()
            .WithBurst()
            .WithReadOnly(hashMap)
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

    protected override void OnDestroy()
    {
        if (m_DamageToHumansHashMap.IsCreated)
            m_DamageToHumansHashMap.Dispose();
    }
}
