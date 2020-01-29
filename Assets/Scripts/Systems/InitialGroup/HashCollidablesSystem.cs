using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[UpdateInGroup(typeof(InitialGroup))]
public class HashCollidablesSystem : JobComponentSystem
{
    private EntityQuery m_StaticCollidableGroup;
    private EntityQuery m_DynamicCollidableGroup;

    public NativeMultiHashMap<int, int> m_StaticCollidableHashMap;
    public JobHandle m_StaticCollidableJobHandle;
    public NativeMultiHashMap<int, int> m_DynamicCollidableHashMap;
    public JobHandle m_DynamicCollidableJobHandle;

    [BurstCompile]
    struct HashGridPositionsJob : IJobForEachWithEntity<GridPosition>
    {
        public NativeMultiHashMap<int, int>.ParallelWriter hashMap;

        public void Execute(Entity entity, int index, [ReadOnly] ref GridPosition gridPosition)
        {
            var hash = GridHash.Hash(gridPosition.Value);
            hashMap.Add(hash, index);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_StaticCollidableJobHandle = inputDeps;

        if (m_StaticCollidableGroup.CalculateEntityCount() != 0)
        {
            if (m_StaticCollidableHashMap.IsCreated)
                m_StaticCollidableHashMap.Dispose();

            m_StaticCollidableHashMap = new NativeMultiHashMap<int, int>(m_StaticCollidableGroup.CalculateEntityCount(), Allocator.Persistent);

            var hashStaticCollidableGridPositionsJob = new HashGridPositionsJob
            {
                hashMap = m_StaticCollidableHashMap.AsParallelWriter(),
            };
            m_StaticCollidableJobHandle = hashStaticCollidableGridPositionsJob.Schedule(m_StaticCollidableGroup, inputDeps);
        }

        m_DynamicCollidableJobHandle = inputDeps;
        if (m_DynamicCollidableGroup.CalculateEntityCount() != 0)
        {
            if (m_DynamicCollidableHashMap.IsCreated)
                m_DynamicCollidableHashMap.Dispose();

            m_DynamicCollidableHashMap = new NativeMultiHashMap<int, int>(m_DynamicCollidableGroup.CalculateEntityCount(), Allocator.Persistent);

            var hashDynamicCollidablePositionsJob = new HashGridPositionsJob
            {
                hashMap = m_DynamicCollidableHashMap.AsParallelWriter(),
            };
            m_DynamicCollidableJobHandle = hashDynamicCollidablePositionsJob.Schedule(m_DynamicCollidableGroup, inputDeps);
        }

        return JobHandle.CombineDependencies(m_StaticCollidableJobHandle, m_DynamicCollidableJobHandle);
    }

    protected override void OnCreate()
    {
        m_StaticCollidableGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(StaticCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_StaticCollidableGroup.AddChangedVersionFilter(typeof(StaticCollidable));

        m_DynamicCollidableGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(DynamicCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnDestroy()
    {
        if (m_StaticCollidableHashMap.IsCreated)
            m_StaticCollidableHashMap.Dispose();
        if (m_DynamicCollidableHashMap.IsCreated)
            m_DynamicCollidableHashMap.Dispose();
    }
}
