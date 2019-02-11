using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using Unity.Collections;

public class LineOfSightSystem : JobComponentSystem
{
    private ComponentGroup m_StaticCollidableGroup;
    private ComponentGroup m_DynamicCollidableGroup;
    private ComponentGroup m_LineOfSightGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> staticCollidableHashMap;
        public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        public NativeMultiHashMap<int, int> unitsLineOfSightHashMap;
    }

    [BurstCompile]
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> gridPositions;
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

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        NativeMultiHashMap<int, int> staticCollidableHashMap;

        var dynamicCollidableGridPositions = m_DynamicCollidableGroup.GetComponentDataArray<GridPosition>();
        var dynamicCollidableCount = dynamicCollidableGridPositions.Length;
        var dynamicCollidableHashMap = new NativeMultiHashMap<int, int>(dynamicCollidableCount, Allocator.TempJob);

        var unitsLineOfSight = m_LineOfSightGroup.GetComponentDataArray<LineOfSight>();
        var unitsLineOfSightCount = unitsLineOfSight.Length;
        var unitsLineOfSightHashMap = new NativeMultiHashMap<int, int>(unitsLineOfSightCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            staticCollidableHashMap = m_PrevGridState.staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            unitsLineOfSightHashMap = unitsLineOfSightHashMap,
        };

        JobHandle hashStaticCollidablePositionsJobHandle = inputDeps;
        if (m_PrevGridState.staticCollidableHashMap.IsCreated)
        {
            staticCollidableHashMap = m_PrevGridState.staticCollidableHashMap;
        }
        else
        {
            ComponentDataArray<GridPosition> staticCollidableGridPositions = m_StaticCollidableGroup.GetComponentDataArray<GridPosition>();
            var staticCollidableCount = staticCollidableGridPositions.Length;
            nextGridState.staticCollidableHashMap = staticCollidableHashMap = new NativeMultiHashMap<int, int>(staticCollidableCount, Allocator.Persistent);

            var hashStaticCollidablePositionsJob = new HashGridPositionsJob
            {
                gridPositions = staticCollidableGridPositions,
                hashMap = staticCollidableHashMap.ToConcurrent(),
            };
            hashStaticCollidablePositionsJobHandle = hashStaticCollidablePositionsJob.Schedule(staticCollidableCount, 64, inputDeps);
        }

        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
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
            staticCollidableHashMap = staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,

        };
        var moveAwayFromUnitsJobHandle = moveAwayFromUnitsJob.Schedule(unitsLineOfSightCount, 64, moveBarrier);

        return moveAwayFromUnitsJobHandle;
    }

    protected override void OnCreateManager()
    {
        m_StaticCollidableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(StaticCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_DynamicCollidableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(DynamicCollidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );

        m_LineOfSightGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(LineOfSight))
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.staticCollidableHashMap.IsCreated)
            m_PrevGridState.staticCollidableHashMap.Dispose();
        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
        if (m_PrevGridState.unitsLineOfSightHashMap.IsCreated)
            m_PrevGridState.unitsLineOfSightHashMap.Dispose();
    }
}
