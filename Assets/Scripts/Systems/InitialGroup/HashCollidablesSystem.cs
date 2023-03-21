using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using UnityEngine;

public struct StaticCollidableComponent : IComponentData
{
    public JobHandle Handle;
    public NativeParallelHashMap<int, int> HashMap;
}

public struct DynamicCollidableComponent : IComponentData
{
    public JobHandle Handle;
    public NativeParallelHashMap<int, int> HashMap;
}

[UpdateInGroup(typeof(InitialGroup))]
public partial struct HashCollidablesSystem : ISystem, ISystemStartStop
{
    private EntityQuery _staticCollidableEntityQuery;
    private EntityQuery _dynamicCollidableEntityQuery;

    public void OnCreate(ref SystemState state)
    {
        state.EntityManager.CreateEntity(typeof(StaticCollidableComponent));
        state.EntityManager.CreateEntity(typeof(DynamicCollidableComponent));

        _staticCollidableEntityQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<StaticCollidable, GridPosition>());
        _staticCollidableEntityQuery.SetChangedVersionFilter(typeof(GridPosition));

        _dynamicCollidableEntityQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<DynamicCollidable, GridPosition>());

        state.RequireAnyForUpdate(_staticCollidableEntityQuery, _dynamicCollidableEntityQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var staticCollidableComponent = SystemAPI.GetSingletonRW<StaticCollidableComponent>();
        var dynamicCollidableComponent = SystemAPI.GetSingletonRW<DynamicCollidableComponent>();

        staticCollidableComponent.ValueRW.Handle = state.Dependency;
        dynamicCollidableComponent.ValueRW.Handle = state.Dependency;

        var staticCollidableCount = _staticCollidableEntityQuery.CalculateEntityCount();
        if (staticCollidableCount != 0)
        {
            if (SystemAPI.GetSingletonRW<StaticCollidableComponent>().ValueRO.HashMap.IsCreated)
                SystemAPI.GetSingletonRW<StaticCollidableComponent>().ValueRW.HashMap.Dispose();

            var hashMap = new NativeParallelHashMap<int, int>(staticCollidableCount, Allocator.Persistent);
            staticCollidableComponent.ValueRW.Handle = new HashGridPositionsJob
            {
                parallelWriter = hashMap.AsParallelWriter()
            }.ScheduleParallel(_staticCollidableEntityQuery, state.Dependency);

            SystemAPI.GetSingletonRW<StaticCollidableComponent>().ValueRW.HashMap = hashMap;
        }

        int dynamicCollidableCount = _dynamicCollidableEntityQuery.CalculateEntityCount();
        if (dynamicCollidableCount != 0)
        {
            if (SystemAPI.GetSingletonRW<DynamicCollidableComponent>().ValueRO.HashMap.IsCreated)
                SystemAPI.GetSingletonRW<DynamicCollidableComponent>().ValueRW.HashMap.Dispose();

            var hashMap = new NativeParallelHashMap<int, int>(dynamicCollidableCount, Allocator.Persistent);
            dynamicCollidableComponent.ValueRW.Handle = new HashGridPositionsJob
            {
                parallelWriter = hashMap.AsParallelWriter()
            }.ScheduleParallel(_dynamicCollidableEntityQuery, state.Dependency);

            SystemAPI.GetSingletonRW<DynamicCollidableComponent>().ValueRW.HashMap = hashMap;
        }

        state.Dependency = JobHandle.CombineDependencies(staticCollidableComponent.ValueRO.Handle, staticCollidableComponent.ValueRO.Handle);
    }

    public void OnStartRunning(ref SystemState state)
    {

    }

    public void OnStopRunning(ref SystemState state)
    {
        var staticCollidableHashMap = SystemAPI.GetSingleton<StaticCollidableComponent>().HashMap;
        if (staticCollidableHashMap.IsCreated)
            staticCollidableHashMap.Dispose();

        var dynamicCollidableHashMap = SystemAPI.GetSingleton<DynamicCollidableComponent>().HashMap;
        if (dynamicCollidableHashMap.IsCreated)
            dynamicCollidableHashMap.Dispose();
    }
}
