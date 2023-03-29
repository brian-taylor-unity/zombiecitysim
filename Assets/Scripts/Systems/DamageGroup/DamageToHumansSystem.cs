using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[UpdateInGroup(typeof(DamageGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct DamageToHumansSystem : ISystem
{
    private EntityQuery _humansQuery;
    private EntityQuery _zombiesQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _humansQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Human, MaxHealth, GridPosition>()
            .WithAllRW<Health, CharacterColor>()
        );
        _zombiesQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Zombie, GridPosition, Damage, TurnActive>()
        );
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var humanCount = _humansQuery.CalculateEntityCount();
        var zombieCount = _zombiesQuery.CalculateEntityCount();

        if (humanCount == 0 || zombieCount == 0)
            return;

        var humanHashMap = new NativeParallelHashMap<int, int>(humanCount, Allocator.TempJob);
        var damageToHumansHashMap = zombieCount < humanCount ?
            new NativeParallelMultiHashMap<int, int>(zombieCount * 8, Allocator.TempJob) :
            new NativeParallelMultiHashMap<int, int>(humanCount * 8, Allocator.TempJob);

        state.Dependency = new HashGridPositionsJob { ParallelWriter = humanHashMap.AsParallelWriter() }.ScheduleParallel(_humansQuery, state.Dependency);
        state.Dependency = new CalculateDamageJob
        {
            DamageTakingHashMap = humanHashMap,
            DamageAmountHashMapParallelWriter = damageToHumansHashMap.AsParallelWriter()
        }.ScheduleParallel(_zombiesQuery, state.Dependency);
        humanHashMap.Dispose(state.Dependency);

        state.Dependency = new DealDamageJob { FullHealthColor = HumanCreator.GetFullHealthColor(), DamageAmountHashMap = damageToHumansHashMap }.ScheduleParallel(_humansQuery, state.Dependency);
        damageToHumansHashMap.Dispose(state.Dependency);
    }
}
