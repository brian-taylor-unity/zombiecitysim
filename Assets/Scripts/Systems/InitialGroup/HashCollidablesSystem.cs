using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitialGroup))]
public class HashCollidablesSystem : SystemBase
{
    private EntityQuery m_StaticCollidableEntityQuery;
    private EntityQuery m_DynamicCollidableEntityQuery;

    public NativeHashMap<int, int> m_StaticCollidableHashMap;
    public JobHandle m_StaticCollidableHashMapJobHandle;
    public NativeHashMap<int, int> m_DynamicCollidableHashMap;
    public JobHandle m_DynamicCollidableHashMapJobHandle;

    protected override void OnUpdate()
    {
        m_StaticCollidableHashMapJobHandle = Dependency;
        m_DynamicCollidableHashMapJobHandle = Dependency;

        int staticCollidableCount = m_StaticCollidableEntityQuery.CalculateEntityCount();
        if (staticCollidableCount != 0)
        {
            if (m_StaticCollidableHashMap.IsCreated)
                m_StaticCollidableHashMap.Dispose();

            m_StaticCollidableHashMap = new NativeHashMap<int, int>(staticCollidableCount, Allocator.Persistent);
            var parallelWriter = m_StaticCollidableHashMap.AsParallelWriter();

            m_StaticCollidableHashMapJobHandle = Entities
                .WithName("HashStaticCollidables")
                .WithAll<StaticCollidable>()
                .WithChangeFilter<StaticCollidable>()
                .WithStoreEntityQueryInField(ref m_StaticCollidableEntityQuery)
                .WithBurst()
                .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                    {
                        var hash = (int)math.hash(gridPosition.Value);
                        parallelWriter.TryAdd(hash, entityInQueryIndex);
                    })
                .ScheduleParallel(Dependency);
        }

        int dynamicCollidableCount = m_DynamicCollidableEntityQuery.CalculateEntityCount();
        if (dynamicCollidableCount != 0)
        {
            if (m_DynamicCollidableHashMap.IsCreated)
                m_DynamicCollidableHashMap.Dispose();

            m_DynamicCollidableHashMap = new NativeHashMap<int, int>(dynamicCollidableCount, Allocator.Persistent);
            var parallelWriter = m_DynamicCollidableHashMap.AsParallelWriter();

            m_DynamicCollidableHashMapJobHandle = Entities
                .WithName("HashDynamicCollidables")
                .WithAll<DynamicCollidable>()
                .WithStoreEntityQueryInField(ref m_DynamicCollidableEntityQuery)
                .WithBurst()
                .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    parallelWriter.TryAdd(hash, entityInQueryIndex);
                })
                .ScheduleParallel(Dependency);
        }

        Dependency = JobHandle.CombineDependencies(m_StaticCollidableHashMapJobHandle, m_DynamicCollidableHashMapJobHandle);
    }

    protected override void OnDestroy()
    {
        if (m_StaticCollidableHashMap.IsCreated)
            m_StaticCollidableHashMap.Dispose();
        if (m_DynamicCollidableHashMap.IsCreated)
            m_DynamicCollidableHashMap.Dispose();
    }
}
