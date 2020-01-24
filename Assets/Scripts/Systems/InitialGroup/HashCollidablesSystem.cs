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
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeMultiHashMap<int, int>.ParallelWriter hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(gridPositions[index].Value);
            hashMap.Add(hash, index);
        }
    }

    [BurstCompile]
    struct DisposeJob : IJob
    {
        [DeallocateOnJobCompletion] public NativeArray<GridPosition> nativeArray;
        public void Execute()
        {
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_StaticCollidableJobHandle = inputDeps;
        if (m_StaticCollidableGroup.CalculateEntityCount() != 0)
        {
            if (m_StaticCollidableHashMap.IsCreated)
                m_StaticCollidableHashMap.Dispose();

            var staticCollidableGridPositions = m_StaticCollidableGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
            var staticCollidableCount = staticCollidableGridPositions.Length;
            m_StaticCollidableHashMap = new NativeMultiHashMap<int, int>(staticCollidableCount, Allocator.Persistent);

            var hashStaticCollidableGridPositionsJob = new HashGridPositionsJob
            {
                gridPositions = staticCollidableGridPositions,
                hashMap = m_StaticCollidableHashMap.AsParallelWriter(),
            };
            m_StaticCollidableJobHandle = hashStaticCollidableGridPositionsJob.Schedule(staticCollidableCount, 64, inputDeps);

            var disposeJob = new DisposeJob
            {
                nativeArray = staticCollidableGridPositions
            };
            m_StaticCollidableJobHandle = disposeJob.Schedule(m_StaticCollidableJobHandle);
        }

        m_DynamicCollidableJobHandle = inputDeps;
        if (m_DynamicCollidableGroup.CalculateEntityCount() != 0)
        {
            if (m_DynamicCollidableHashMap.IsCreated)
                m_DynamicCollidableHashMap.Dispose();

            var dynamicCollidableGridPositions = m_DynamicCollidableGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
            var dynamicCollidableCount = dynamicCollidableGridPositions.Length;
            m_DynamicCollidableHashMap = new NativeMultiHashMap<int, int>(dynamicCollidableCount, Allocator.Persistent);

            var hashDynamicCollidablePositionsJob = new HashGridPositionsJob
            {
                gridPositions = dynamicCollidableGridPositions,
                hashMap = m_DynamicCollidableHashMap.AsParallelWriter(),
            };
            m_DynamicCollidableJobHandle = hashDynamicCollidablePositionsJob.Schedule(dynamicCollidableCount, 64, inputDeps);

            var disposeJob = new DisposeJob
            {
                nativeArray = dynamicCollidableGridPositions
            };
            m_DynamicCollidableJobHandle = disposeJob.Schedule(m_DynamicCollidableJobHandle);
        }

        return JobHandle.CombineDependencies(m_StaticCollidableJobHandle, m_DynamicCollidableJobHandle);
    }

    protected override void OnCreate()
    {
        m_StaticCollidableGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(StaticCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_StaticCollidableGroup.SetFilterChanged(typeof(StaticCollidable));

        m_DynamicCollidableGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(DynamicCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnStopRunning()
    {
        if (m_StaticCollidableHashMap.IsCreated)
            m_StaticCollidableHashMap.Dispose();
        if (m_DynamicCollidableHashMap.IsCreated)
            m_DynamicCollidableHashMap.Dispose();
    }
}
