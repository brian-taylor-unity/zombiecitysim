using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateAfter(typeof(KillAndSpawnSystem))]
public class DamageToHumansSystem : SystemBase
{
    private EntityQuery humansQuery;
    private EntityQuery zombiesQuery;

    protected override void OnUpdate()
    {
        var humanCount = humansQuery.CalculateEntityCount();
        var zombieCount = zombiesQuery.CalculateEntityCount();

        if (humanCount == 0 || zombieCount == 0)
            return;

        var humanHashMap = new NativeHashMap<int, int>(humanCount, Allocator.TempJob);
        NativeMultiHashMap<int, int> damageHashMap;
        if (zombieCount < humanCount)
            damageHashMap = new NativeMultiHashMap<int, int>(zombieCount * 8, Allocator.TempJob);
        else
            damageHashMap = new NativeMultiHashMap<int, int>(humanCount * 8, Allocator.TempJob);

        var humanHashMapParallelWriter = humanHashMap.AsParallelWriter();

        Entities
            .WithName("HashHumans")
            .WithStoreEntityQueryInField(ref humansQuery)
            .WithAll<Human>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
            {
                var hash = (int)math.hash(gridPosition.Value);
                humanHashMapParallelWriter.TryAdd(hash, entityInQueryIndex);
            })
            .ScheduleParallel();

        var damageHashMapParallelWriter = damageHashMap.AsParallelWriter();

        Entities
            .WithName("CalculateDamageFromZombies")
            .WithStoreEntityQueryInField(ref zombiesQuery)
            .WithAll<Zombie>()
            .WithChangeFilter<TurnsUntilActive>()
            .WithReadOnly(humanHashMap)
            .WithDisposeOnCompletion(humanHashMap)
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
            .ScheduleParallel();

        var humanMaxHealth = GameController.instance.humanStartingHealth;

        Entities
            .WithName("DealDamageToHumans")
            .WithAll<Human>()
            .WithReadOnly(damageHashMap)
            .WithDisposeOnCompletion(damageHashMap)
            .WithBurst()
            .ForEach((ref Health health, ref CharacterColor materialColor, in GridPosition gridPosition) =>
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

                        var lerp = math.lerp(0.0f, 1.0f, (float)myHealth / humanMaxHealth);
                        materialColor.Value = new float4(1.0f - lerp, lerp, 0.0f, 1.0f);
                        health.Value = myHealth;
                    }
                })
            .ScheduleParallel();
    }
}
