using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

public struct StaticCollidableHashMapComponent : IComponentData
{
    public NativeParallelHashMap<int, int> Value;
}

public struct DynamicCollidableHashMapComponent : IComponentData
{
    public NativeParallelHashMap<int, int> Value;
}

[UpdateInGroup(typeof(InitialGroup))]
public partial class HashCollidablesSystem : SystemBase
{
    private EntityQuery _staticCollidableEntityQuery;
    private EntityQuery _dynamicCollidableEntityQuery;

    public JobHandle StaticCollidableHashMapJobHandle;
    public JobHandle DynamicCollidableHashMapJobHandle;

    protected override void OnCreate()
    {
        EntityManager.CreateEntity(typeof(StaticCollidableHashMapComponent));
        EntityManager.CreateEntity(typeof(DynamicCollidableHashMapComponent));

        RequireAnyForUpdate(_staticCollidableEntityQuery, _dynamicCollidableEntityQuery);
    }

    protected override void OnUpdate()
    {
        StaticCollidableHashMapJobHandle = Dependency;
        DynamicCollidableHashMapJobHandle = Dependency;

        var staticCollidableCount = _staticCollidableEntityQuery.CalculateEntityCount();
        if (staticCollidableCount != 0)
        {
            if (SystemAPI.GetSingletonRW<StaticCollidableHashMapComponent>().ValueRW.Value.IsCreated)
                SystemAPI.GetSingletonRW<StaticCollidableHashMapComponent>().ValueRW.Value.Dispose();

            SystemAPI.GetSingletonRW<StaticCollidableHashMapComponent>().ValueRW.Value = new NativeParallelHashMap<int, int>(staticCollidableCount, Allocator.Persistent);
            var parallelWriter = SystemAPI.GetSingletonRW<StaticCollidableHashMapComponent>().ValueRW.Value.AsParallelWriter();

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
            if (SystemAPI.GetSingletonRW<DynamicCollidableHashMapComponent>().ValueRW.Value.IsCreated)
                SystemAPI.GetSingletonRW<DynamicCollidableHashMapComponent>().ValueRW.Value.Dispose();

            SystemAPI.GetSingletonRW<DynamicCollidableHashMapComponent>().ValueRW.Value = new NativeParallelHashMap<int, int>(dynamicCollidableCount, Allocator.Persistent);
            var parallelWriter = SystemAPI.GetSingletonRW<DynamicCollidableHashMapComponent>().ValueRW.Value.AsParallelWriter();

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
        var staticCollidableHashMap = SystemAPI.GetSingleton<StaticCollidableHashMapComponent>().Value;
        if (staticCollidableHashMap.IsCreated)
            staticCollidableHashMap.Dispose();

        var dynamicCollidableHashMap = SystemAPI.GetSingleton<DynamicCollidableHashMapComponent>().Value;
        if (dynamicCollidableHashMap.IsCreated)
            dynamicCollidableHashMap.Dispose();
    }
}
