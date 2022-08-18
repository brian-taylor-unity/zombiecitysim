using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitialGroup))]
public partial class HashCollidablesSystem : SystemBase
{
    private EntityQuery _staticCollidableEntityQuery;
    private EntityQuery _dynamicCollidableEntityQuery;

    public NativeParallelHashMap<int, int> StaticCollidableHashMap;
    public JobHandle StaticCollidableHashMapJobHandle;
    public NativeParallelHashMap<int, int> DynamicCollidableHashMap;
    public JobHandle DynamicCollidableHashMapJobHandle;

    protected override void OnUpdate()
    {
        StaticCollidableHashMapJobHandle = Dependency;
        DynamicCollidableHashMapJobHandle = Dependency;

        var staticCollidableCount = _staticCollidableEntityQuery.CalculateEntityCount();
        if (staticCollidableCount != 0)
        {
            if (StaticCollidableHashMap.IsCreated)
                StaticCollidableHashMap.Dispose();

            StaticCollidableHashMap = new NativeParallelHashMap<int, int>(staticCollidableCount, Allocator.Persistent);
            var parallelWriter = StaticCollidableHashMap.AsParallelWriter();

            StaticCollidableHashMapJobHandle = Entities
                .WithName("HashStaticCollidables")
                .WithAll<StaticCollidable>()
                .WithChangeFilter<StaticCollidable>()
                .WithStoreEntityQueryInField(ref _staticCollidableEntityQuery)
                .WithBurst()
                .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                    {
                        var hash = (int)math.hash(gridPosition.Value);
                        parallelWriter.TryAdd(hash, entityInQueryIndex);
                    })
                .ScheduleParallel(Dependency);
        }

        int dynamicCollidableCount = _dynamicCollidableEntityQuery.CalculateEntityCount();
        if (dynamicCollidableCount != 0)
        {
            if (DynamicCollidableHashMap.IsCreated)
                DynamicCollidableHashMap.Dispose();

            DynamicCollidableHashMap = new NativeParallelHashMap<int, int>(dynamicCollidableCount, Allocator.Persistent);
            var parallelWriter = DynamicCollidableHashMap.AsParallelWriter();

            DynamicCollidableHashMapJobHandle = Entities
                .WithName("HashDynamicCollidables")
                .WithAll<DynamicCollidable>()
                .WithStoreEntityQueryInField(ref _dynamicCollidableEntityQuery)
                .WithBurst()
                .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    parallelWriter.TryAdd(hash, entityInQueryIndex);
                })
                .ScheduleParallel(Dependency);
        }

        Dependency = JobHandle.CombineDependencies(StaticCollidableHashMapJobHandle, DynamicCollidableHashMapJobHandle);
    }

    protected override void OnStopRunning()
    {
        if (StaticCollidableHashMap.IsCreated)
            StaticCollidableHashMap.Dispose();
        if (DynamicCollidableHashMap.IsCreated)
            DynamicCollidableHashMap.Dispose();
    }
}
