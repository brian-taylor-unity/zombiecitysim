using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateAfter(typeof(DamageToHumansSystem))]
public class DamageToZombiesSystem : SystemBase
{
    private EntityQuery zombiesQuery;
    private EntityQuery humansQuery;

    protected override void OnUpdate()
    {
        var zombieCount = zombiesQuery.CalculateEntityCount();
        var humanCount = humansQuery.CalculateEntityCount();

        if (zombieCount == 0 || humanCount == 0)
            return;

        var zombieHashMap = new NativeHashMap<int, int>(zombieCount, Allocator.TempJob);
        NativeMultiHashMap<int, int> damageToZombiesHashMap;
        if (humanCount < zombieCount)
            damageToZombiesHashMap = new NativeMultiHashMap<int, int>(humanCount * 8, Allocator.TempJob);
        else
            damageToZombiesHashMap = new NativeMultiHashMap<int, int>(zombieCount * 8, Allocator.TempJob);

        var zombieHashMapParallelWriter = zombieHashMap.AsParallelWriter();

        Entities
            .WithName("HashZombies")
            .WithStoreEntityQueryInField(ref zombiesQuery)
            .WithAll<Zombie>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    zombieHashMapParallelWriter.TryAdd(hash, entityInQueryIndex);
                })
            .ScheduleParallel();

        var damageHashMap = damageToZombiesHashMap;
        var damageHashMapParallelWriter = damageHashMap.AsParallelWriter();

        Entities
            .WithName("CalculateDamageFromHumans")
            .WithStoreEntityQueryInField(ref humansQuery)
            .WithAll<Human>()
            .WithChangeFilter<TurnsUntilActive>()
            .WithReadOnly(zombieHashMap)
            .WithDisposeOnCompletion(zombieHashMap)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in TurnsUntilActive turnsUntilActive, in GridPosition gridPosition, in Damage damage) =>
                {
                    if (turnsUntilActive.Value != 1)
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
            .ScheduleParallel();

        var zombieMaxHealth = GameController.instance.zombieStartingHealth;

        Entities
            .WithName("DealDamageToZombies")
            .WithAll<Zombie>()
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

                        var lerp = math.lerp(0.0f, 1.0f, (float)myHealth / zombieMaxHealth);
                        materialColor.Value = new float4(lerp, 1.0f - lerp, 0.0f, 1.0f);
                        health.Value = myHealth;
                    }
                })
            .ScheduleParallel();
    }
}
