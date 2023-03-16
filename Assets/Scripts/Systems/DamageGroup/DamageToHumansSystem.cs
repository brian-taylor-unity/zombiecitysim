using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(DamageGroup))]
[UpdateAfter(typeof(KillAndSpawnSystem))]
public partial struct DamageToHumansSystem : ISystem
{
    private EntityQuery humansQuery;
    private EntityQuery zombiesQuery;

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        humansQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Human>()
            .Build(ref state);
        zombiesQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Zombie>()
            .Build(ref state);
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        var humanCount = humansQuery.CalculateEntityCount();
        var zombieCount = zombiesQuery.CalculateEntityCount();

        if (humanCount == 0 || zombieCount == 0)
            return;

        var humanHashMap = new NativeParallelHashMap<int, int>(humanCount, Allocator.TempJob);
        var damageHashMap = zombieCount < humanCount ?
            new NativeParallelMultiHashMap<int, int>(zombieCount * 8, Allocator.TempJob) :
            new NativeParallelMultiHashMap<int, int>(humanCount * 8, Allocator.TempJob);

        state.Dependency = new HashGridPositionsJob { parallelWriter = humanHashMap.AsParallelWriter() }.ScheduleParallel(humansQuery, state.Dependency);
        state.Dependency = new CalculateDamageJob { DamageTakingHashMap = humanHashMap, DamageAmountHashMapParallelWriter = damageHashMap.AsParallelWriter() }.ScheduleParallel(zombiesQuery, state.Dependency);
        humanHashMap.Dispose(state.Dependency);

        state.Dependency = new DealDamageJob { DamageAmountHashMap = damageHashMap }.ScheduleParallel(humansQuery, state.Dependency);
        damageHashMap.Dispose(state.Dependency);
    }
}
