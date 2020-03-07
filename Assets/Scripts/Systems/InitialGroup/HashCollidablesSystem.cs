﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitialGroup))]
public class HashCollidablesSystem : JobComponentSystem
{
    private EntityQuery m_StaticCollidableEntityQuery;
    private EntityQuery m_DynamicCollidableEntityQuery;

    public NativeHashMap<int, int> m_StaticCollidableHashMap;
    public JobHandle m_StaticCollidableJobHandle;
    public NativeHashMap<int, int> m_DynamicCollidableHashMap;
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

            m_StaticCollidableHashMap = new NativeHashMap<int, int>(staticCollidableCount, Allocator.Persistent);
            var parallelWriter = m_StaticCollidableHashMap.AsParallelWriter();

            m_StaticCollidableJobHandle = Entities
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
                .Schedule(inputDeps);
        }

        int dynamicCollidableCount = m_DynamicCollidableEntityQuery.CalculateEntityCount();
        if (dynamicCollidableCount != 0)
        {
            if (m_DynamicCollidableHashMap.IsCreated)
                m_DynamicCollidableHashMap.Dispose();

            m_DynamicCollidableHashMap = new NativeHashMap<int, int>(dynamicCollidableCount, Allocator.Persistent);
            var parallelWriter = m_DynamicCollidableHashMap.AsParallelWriter();

            m_DynamicCollidableJobHandle = Entities
                .WithName("HashDynamicCollidables")
                .WithAll<DynamicCollidable>()
                .WithStoreEntityQueryInField(ref m_DynamicCollidableEntityQuery)
                .WithBurst()
                .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    parallelWriter.TryAdd(hash, entityInQueryIndex);
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
