using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

public class LineOfSightSystem : JobComponentSystem
{
    private EntityQuery m_StaticCollidableGroup;
    private EntityQuery m_DynamicCollidableGroup;
    private EntityQuery m_LineOfSightGroup;

    private NativeMultiHashMap<int, int> m_StaticCollidableHashMap;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> dynamicCollidableGridPositions;
        public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        public NativeArray<GridPosition> unitsLineOfSight;
        public NativeMultiHashMap<int, int> unitsLineOfSightHashMap;
    }

    [BurstCompile]
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeMultiHashMap<int, int>.Concurrent hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(gridPositions[index].Value);
            hashMap.Add(hash, index);
        }
    }

    [BurstCompile]
    struct MoveAwayFromUnitsJob : IJobParallelFor
    {
        [ReadOnly] public NativeMultiHashMap<int, int> staticCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> dynamicCollidableHashMap;

        public void Execute(int index)
        {

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
        var dynamicCollidableGridPositions = m_DynamicCollidableGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var dynamicCollidableCount = dynamicCollidableGridPositions.Length;
        var dynamicCollidableHashMap = new NativeMultiHashMap<int, int>(dynamicCollidableCount, Allocator.TempJob);

        var unitsLineOfSight = m_LineOfSightGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var unitsLineOfSightCount = unitsLineOfSight.Length;
        var unitsLineOfSightHashMap = new NativeMultiHashMap<int, int>(unitsLineOfSightCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            dynamicCollidableGridPositions = dynamicCollidableGridPositions,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            unitsLineOfSight = unitsLineOfSight,
            unitsLineOfSightHashMap = unitsLineOfSightHashMap,
        };

        JobHandle hashStaticCollidablePositionsJobHandle = inputDeps;
        if (m_StaticCollidableGroup.CalculateLength() != 0)
        {
            if (m_StaticCollidableHashMap.IsCreated)
                m_StaticCollidableHashMap.Dispose();

            var staticCollidableGridPositions = m_StaticCollidableGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
            var staticCollidableCount = staticCollidableGridPositions.Length;
            m_StaticCollidableHashMap = new NativeMultiHashMap<int, int>(staticCollidableCount, Allocator.Persistent);

            var hashStaticCollidablePositionsJob = new HashGridPositionsJob
            {
                gridPositions = staticCollidableGridPositions,
                hashMap = m_StaticCollidableHashMap.ToConcurrent(),
            };
            hashStaticCollidablePositionsJobHandle = hashStaticCollidablePositionsJob.Schedule(staticCollidableCount, 64, inputDeps);

            var disposeJob = new DisposeJob
            {
                nativeArray = staticCollidableGridPositions
            };
            hashStaticCollidablePositionsJobHandle = disposeJob.Schedule(hashStaticCollidablePositionsJobHandle);
        }

        if (m_PrevGridState.dynamicCollidableGridPositions.IsCreated)
            m_PrevGridState.dynamicCollidableGridPositions.Dispose();
        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
        if (m_PrevGridState.unitsLineOfSight.IsCreated)
            m_PrevGridState.unitsLineOfSight.Dispose();
        if (m_PrevGridState.unitsLineOfSightHashMap.IsCreated)
            m_PrevGridState.unitsLineOfSightHashMap.Dispose();
        m_PrevGridState = nextGridState;

        var hashDynamicCollidablePositionsJob = new HashGridPositionsJob
        {
            gridPositions = dynamicCollidableGridPositions,
            hashMap = dynamicCollidableHashMap.ToConcurrent(),
        };
        var hashDynamicCollidablePositionsJobHandle = hashDynamicCollidablePositionsJob.Schedule(dynamicCollidableCount, 64, inputDeps);

        var moveBarrier = JobHandle.CombineDependencies(hashStaticCollidablePositionsJobHandle, hashDynamicCollidablePositionsJobHandle);

        var moveAwayFromUnitsJob = new MoveAwayFromUnitsJob
        {
            staticCollidableHashMap = m_StaticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,

        };
        var moveAwayFromUnitsJobHandle = moveAwayFromUnitsJob.Schedule(unitsLineOfSightCount, 64, moveBarrier);

        return moveAwayFromUnitsJobHandle;
    }

    protected override void OnCreateManager()
    {
        m_StaticCollidableGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(StaticCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_DynamicCollidableGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(DynamicCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );

        m_LineOfSightGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(LineOfSight)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_StaticCollidableHashMap.IsCreated)
            m_StaticCollidableHashMap.Dispose();
        if (m_PrevGridState.dynamicCollidableGridPositions.IsCreated)
            m_PrevGridState.dynamicCollidableGridPositions.Dispose();
        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
        if (m_PrevGridState.unitsLineOfSight.IsCreated)
            m_PrevGridState.unitsLineOfSight.Dispose();
        if (m_PrevGridState.unitsLineOfSightHashMap.IsCreated)
            m_PrevGridState.unitsLineOfSightHashMap.Dispose();
    }
}
