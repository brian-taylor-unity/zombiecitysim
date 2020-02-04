using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateAfter(typeof(MoveTowardsTargetSystem))]
public class ResolveGridMovementSystem : JobComponentSystem
{
    private EntityQuery m_MoveUnits;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> gridPositionArray;
        public NativeArray<NextGridPosition> nextGridPositionArray;
        public NativeMultiHashMap<int, int> nextGridPositionHashMap;
    }

    [BurstCompile]
    struct HashNextGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<NextGridPosition> nextGridPositions;
        public NativeMultiHashMap<int, int>.ParallelWriter hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(nextGridPositions[index].Value);
            hashMap.Add(hash, index);
        }
    }

    [BurstCompile]
    struct ResolveCollidedMovementJob : IJobNativeMultiHashMapMergedSharedKeyIndices
    {
        [ReadOnly] public NativeArray<NextGridPosition> nextGridPositionArray;
        public NativeArray<GridPosition> gridPositionArray;

        public void ExecuteFirst(int index)
        {
            // This was the first unit added
            gridPositionArray[index] = new GridPosition { Value = nextGridPositionArray[index].Value };
        }

        public void ExecuteNext(int innerIndex, int index)
        {
            // Don't move this unit
        }
    }

    [BurstCompile]
    struct WriteEntityDataJob : IJobForEachWithEntity<GridPosition, NextGridPosition>
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositionArray;

        public void Execute(Entity entity, int index, [ReadOnly] ref GridPosition gridPosition, ref NextGridPosition nextGridPosition)
        {
            nextGridPosition = new NextGridPosition { Value = gridPositionArray[index].Value };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var gridPositionArray = m_MoveUnits.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var nextGridPositionArray = m_MoveUnits.ToComponentDataArray<NextGridPosition>(Allocator.TempJob);
        var nextGridPositionCount = nextGridPositionArray.Length;
        var nextGridPositionHashMap = new NativeMultiHashMap<int, int>(nextGridPositionCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            gridPositionArray = gridPositionArray,
            nextGridPositionArray = nextGridPositionArray,
            nextGridPositionHashMap = nextGridPositionHashMap,
        };
        if (m_PrevGridState.gridPositionArray.IsCreated)
            m_PrevGridState.gridPositionArray.Dispose();
        if (m_PrevGridState.nextGridPositionArray.IsCreated)
            m_PrevGridState.nextGridPositionArray.Dispose();
        if (m_PrevGridState.nextGridPositionHashMap.IsCreated)
            m_PrevGridState.nextGridPositionHashMap.Dispose();
        m_PrevGridState = nextGridState;

        var hashNextGridPositionsJob = new HashNextGridPositionsJob
        {
            nextGridPositions = nextGridPositionArray,
            hashMap = nextGridPositionHashMap.AsParallelWriter(),
        };
        var hashNextGridPositionsJobHandle = hashNextGridPositionsJob.Schedule(nextGridPositionCount, 64, inputDeps);

        var resolveCollidedMovementJob = new ResolveCollidedMovementJob
        {
            nextGridPositionArray = nextGridPositionArray,
            gridPositionArray = gridPositionArray,
        };
        var resolveCollidedMovementJobHandle = resolveCollidedMovementJob.Schedule(nextGridPositionHashMap, 64, hashNextGridPositionsJobHandle);

        var writeEntityDataJob = new WriteEntityDataJob
        {
            gridPositionArray = gridPositionArray,
        };
        var writeEntityDataJobHandle = writeEntityDataJob.Schedule(m_MoveUnits, resolveCollidedMovementJobHandle);

        return writeEntityDataJobHandle;
    }

    protected override void OnCreate()
    {
        m_MoveUnits = GetEntityQuery(
            ComponentType.ReadOnly(typeof(GridPosition)),
            typeof(NextGridPosition)
        );
    }

    protected override void OnStopRunning()
    {
        if (m_PrevGridState.gridPositionArray.IsCreated)
            m_PrevGridState.gridPositionArray.Dispose();
        if (m_PrevGridState.nextGridPositionArray.IsCreated)
            m_PrevGridState.nextGridPositionArray.Dispose();
        if (m_PrevGridState.nextGridPositionHashMap.IsCreated)
            m_PrevGridState.nextGridPositionHashMap.Dispose();
    }
}
