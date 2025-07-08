using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

public struct HashRoadsSystemComponent : IComponentData
{
    public JobHandle Handle;
    public NativeParallelHashMap<uint, int> HashMap;
}

[UpdateInGroup(typeof(InitialGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct HashRoadsSystem : ISystem
{
    private EntityQuery _roadEntityQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _roadEntityQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Road, GridPosition>());
        state.RequireForUpdate<HashRoadsSystemComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var hashRoadsSystemComponent = SystemAPI.GetSingletonRW<HashRoadsSystemComponent>();
        hashRoadsSystemComponent.ValueRW.Handle = state.Dependency;
        var roadsCount = _roadEntityQuery.CalculateEntityCount();
        if (SystemAPI.GetSingletonRW<HashRoadsSystemComponent>().ValueRO.HashMap.IsCreated)
            SystemAPI.GetSingletonRW<HashRoadsSystemComponent>().ValueRW.HashMap.Dispose();

        var hashMap = new NativeParallelHashMap<uint, int>(roadsCount, Allocator.Persistent);
        hashRoadsSystemComponent.ValueRW.Handle = new HashGridPositionsJob
        {
            ParallelWriter = hashMap.AsParallelWriter()
        }.ScheduleParallel(_roadEntityQuery, state.Dependency);

        SystemAPI.GetSingletonRW<HashRoadsSystemComponent>().ValueRW.HashMap = hashMap;

        state.Dependency = hashRoadsSystemComponent.ValueRO.Handle;
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {
        if (SystemAPI.HasSingleton<HashRoadsSystemComponent>())
        {
            var roadsHashMap = SystemAPI.GetSingleton<HashRoadsSystemComponent>().HashMap;
            if (roadsHashMap.IsCreated)
                roadsHashMap.Dispose();
        }
    }
}
