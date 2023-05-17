using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[UpdateInGroup(typeof(DamageGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct DamageToZombiesSystem : ISystem
{
    private EntityQuery _zombiesQuery;
    private EntityQuery _humansQuery;
    private float4 _zombieFullHealthColor;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _zombiesQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Zombie, MaxHealth, GridPosition>()
            .WithAllRW<Health, CharacterColor>()
        );
        _humansQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Human, GridPosition, Damage, TurnActive>()
        );
        _zombieFullHealthColor = new float4();
        ZombieCreator.FillFullHealthColor(ref _zombieFullHealthColor);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var zombieCount = _zombiesQuery.CalculateEntityCount();
        var humanCount = _humansQuery.CalculateEntityCount();

        if (zombieCount == 0 || humanCount == 0)
            return;

        var zombieHashMap = new NativeParallelHashMap<uint, int>(zombieCount, Allocator.TempJob);
        var damageToZombiesHashMap = humanCount < zombieCount ?
            new NativeParallelMultiHashMap<uint, int>(humanCount * 8, Allocator.TempJob) :
            new NativeParallelMultiHashMap<uint, int>(zombieCount * 8, Allocator.TempJob);

        state.Dependency = new HashGridPositionsJob { ParallelWriter = zombieHashMap.AsParallelWriter() }.ScheduleParallel(_zombiesQuery, state.Dependency);
        state.Dependency = new CalculateDamageJob
        {
            DamageTakingHashMap = zombieHashMap,
            DamageAmountHashMapParallelWriter = damageToZombiesHashMap.AsParallelWriter()
        }.ScheduleParallel(_humansQuery, state.Dependency);
        zombieHashMap.Dispose(state.Dependency);

        state.Dependency = new DealDamageJob { DamageAmountHashMap = damageToZombiesHashMap, FullHealthColor = _zombieFullHealthColor }.ScheduleParallel(_zombiesQuery, state.Dependency);
        damageToZombiesHashMap.Dispose(state.Dependency);
    }
}
