using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitialGroup))]
public class HashCollidablesSystem : JobComponentSystem
{
    private EntityQuery m_StaticCollidableEntityQuery;
    private EntityQuery m_DynamicCollidableEntityQuery;

    public NativeMultiHashMap<int, int> m_StaticCollidableHashMap;
    public JobHandle m_StaticCollidableJobHandle;
    public NativeMultiHashMap<int, int> m_DynamicCollidableHashMap;
    public JobHandle m_DynamicCollidableJobHandle;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_StaticCollidableJobHandle = inputDeps;
        m_DynamicCollidableJobHandle = inputDeps;

        int staticCollidableCount = m_StaticCollidableEntityQuery.CalculateEntityCount();
        if (staticCollidableCount != 0)
        {
            if (m_StaticCollidableHashMap.IsCreated)
                m_StaticCollidableHashMap.Dispose();

            m_StaticCollidableHashMap = new NativeMultiHashMap<int, int>(staticCollidableCount, Allocator.Persistent);
            var parallelWriter = m_StaticCollidableHashMap.AsParallelWriter();

            m_StaticCollidableJobHandle = Entities
                .WithStoreEntityQueryInField(ref m_StaticCollidableEntityQuery)
                .WithAll<StaticCollidable>()
                .WithChangeFilter<StaticCollidable>()
                .WithBurst()
                .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                    {
                        var hash = (int)math.hash(gridPosition.Value);
                        parallelWriter.Add(hash, entityInQueryIndex);
                    })
                .Schedule(inputDeps);
        }

        int dynamicCollidableCount = m_DynamicCollidableEntityQuery.CalculateEntityCount();
        if (dynamicCollidableCount != 0)
        {
            if (m_DynamicCollidableHashMap.IsCreated)
                m_DynamicCollidableHashMap.Dispose();

            m_DynamicCollidableHashMap = new NativeMultiHashMap<int, int>(dynamicCollidableCount, Allocator.Persistent);
            var parallelWriter = m_DynamicCollidableHashMap.AsParallelWriter();

            m_DynamicCollidableJobHandle = Entities
                .WithStoreEntityQueryInField(ref m_DynamicCollidableEntityQuery)
                .WithAll<DynamicCollidable>()
                .WithBurst()
                .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    parallelWriter.Add(hash, entityInQueryIndex);
                })
                .Schedule(inputDeps);
        }

        return JobHandle.CombineDependencies(m_StaticCollidableJobHandle, m_DynamicCollidableJobHandle);
    }

    protected override void OnDestroy()
    {
        if (m_StaticCollidableHashMap.IsCreated)
            m_StaticCollidableHashMap.Dispose();
        if (m_DynamicCollidableHashMap.IsCreated)
            m_DynamicCollidableHashMap.Dispose();
    }
}
