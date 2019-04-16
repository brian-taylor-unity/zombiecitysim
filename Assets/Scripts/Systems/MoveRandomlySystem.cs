using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class MoveRandomlySystem : JobComponentSystem
{
    private EntityQuery m_StaticCollidableGroup;
    private EntityQuery m_DynamicCollidableGroup;
    private EntityQuery m_MoveRandomlyGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> staticCollidableHashMap;
        public NativeArray<GridPosition> dynamicCollidableGridPositions;
        public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        public NativeArray<GridPosition> moveRandomlyGridPositions;
        public NativeArray<Translation> moveRandomlyTranslations;
        public NativeArray<GridPosition> nextGridPositions;
        public NativeMultiHashMap<int, int> nextGridPositionsHashMap;
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
    struct HashGridPositionsNativeArrayJob : IJobParallelFor
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
    struct MoveRandomlyJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeArray<GridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> staticCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        [ReadOnly] public int tick;

        public void Execute(int index)
        {
            int3 myGridPositionValue = gridPositions[index].Value;

            int upDirKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
            int rightDirKey = GridHash.Hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
            int downDirKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
            int leftDirKey = GridHash.Hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));

            bool upMoveAvail = true;
            bool rightMoveAvail = true;
            bool downMoveAvail = true;
            bool leftMoveAvail = true;

            if (staticCollidableHashMap.TryGetFirstValue(upDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(upDirKey, out _, out _))
                upMoveAvail = false;
            if (staticCollidableHashMap.TryGetFirstValue(rightDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(rightDirKey, out _, out _))
                rightMoveAvail = false;
            if (staticCollidableHashMap.TryGetFirstValue(downDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(downDirKey, out _, out _))
                downMoveAvail = false;
            if (staticCollidableHashMap.TryGetFirstValue(leftDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(leftDirKey, out _, out _))
                leftMoveAvail = false;

            // Pick a random direction to move
            uint seed = (uint)(tick * GridHash.Hash(myGridPositionValue) * index);
            if (seed == 0)
                seed += (uint)(tick + index);

            Random rand = new Random(seed);
            int randomDirIndex = rand.NextInt(0, 4);

            bool moved = false;
            for (int i = 0; i < 4; i++)
            {
                int direction = (randomDirIndex + i) % 4;
                switch (direction)
                {
                    case 0:
                        if (upMoveAvail)
                        {
                            myGridPositionValue.z += 1;
                            moved = true;
                        }
                        break;
                    case 1:
                        if (rightMoveAvail)
                        {
                            myGridPositionValue.x += 1;
                            moved = true;
                        }
                        break;
                    case 2:
                        if (downMoveAvail)
                        {
                            myGridPositionValue.z -= 1;
                            moved = true;
                        }
                        break;
                    case 3:
                        if (leftMoveAvail)
                        {
                            myGridPositionValue.x -= 1;
                            moved = true;
                        }
                        break;
                }

                if (moved)
                    break;
            }
            nextGridPositions[index] = new GridPosition { Value = myGridPositionValue };
        }
    }

    [BurstCompile]
    struct ResolveCollidedMovementJob : IJobNativeMultiHashMapMergedSharedKeyIndices
    {
        [ReadOnly] public NativeArray<GridPosition> updatedGridPositions;
        public NativeArray<GridPosition> gridPositionArray;
        public NativeArray<Translation> translationArray;

        public void ExecuteFirst(int index)
        {
            // This was the first unit added
            gridPositionArray[index] = updatedGridPositions[index];
            translationArray[index] = new Translation { Value = new float3(updatedGridPositions[index].Value) };
        }

        public void ExecuteNext(int innerIndex, int index)
        {
            // Don't move this unit
        }
    }

    [BurstCompile]
    struct WriteEntityDataJob : IJobForEachWithEntity<GridPosition, Translation>
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositionArray;
        [ReadOnly] public NativeArray<Translation> translationArray;

        public void Execute(Entity entity, int index, ref GridPosition gridPosition, ref Translation translation)
        {
            gridPosition = new GridPosition { Value = gridPositionArray[index].Value };
            translation = new Translation { Value = translationArray[index].Value };
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
        NativeMultiHashMap<int, int> staticCollidableHashMap;

        var dynamicCollidableGridPositions = m_DynamicCollidableGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var dynamicCollidableCount = dynamicCollidableGridPositions.Length;
        var dynamicCollidableHashMap = new NativeMultiHashMap<int, int>(dynamicCollidableCount, Allocator.TempJob);

        var moveRandomlyGridPositions = m_MoveRandomlyGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var moveRandomlyPositions = m_MoveRandomlyGroup.ToComponentDataArray<Translation>(Allocator.TempJob);
        var moveRandomlyCount = moveRandomlyGridPositions.Length;
        var nextGridPositions = new NativeArray<GridPosition>(moveRandomlyCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var nextGridPositionsHashMap = new NativeMultiHashMap<int, int>(moveRandomlyCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            staticCollidableHashMap = m_PrevGridState.staticCollidableHashMap,
            dynamicCollidableGridPositions = dynamicCollidableGridPositions,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            moveRandomlyGridPositions = moveRandomlyGridPositions,
            moveRandomlyTranslations = moveRandomlyPositions,
            nextGridPositions = nextGridPositions,
            nextGridPositionsHashMap = nextGridPositionsHashMap,
        };

        JobHandle hashStaticCollidablePositionsJobHandle = inputDeps;
        if (m_PrevGridState.staticCollidableHashMap.IsCreated)
        {
            staticCollidableHashMap = m_PrevGridState.staticCollidableHashMap;
        }
        else
        {
            var staticCollidableGridPositions = m_StaticCollidableGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
            var staticCollidableCount = staticCollidableGridPositions.Length;
            nextGridState.staticCollidableHashMap = staticCollidableHashMap = new NativeMultiHashMap<int, int>(staticCollidableCount, Allocator.Persistent);

            var hashStaticCollidablePositionsJob = new HashGridPositionsJob
            {
                gridPositions = staticCollidableGridPositions,
                hashMap = staticCollidableHashMap.ToConcurrent(),
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
        if (m_PrevGridState.moveRandomlyGridPositions.IsCreated)
            m_PrevGridState.moveRandomlyGridPositions.Dispose();
        if (m_PrevGridState.moveRandomlyTranslations.IsCreated)
            m_PrevGridState.moveRandomlyTranslations.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.nextGridPositionsHashMap.IsCreated)
            m_PrevGridState.nextGridPositionsHashMap.Dispose();
        m_PrevGridState = nextGridState;

        var hashDynamicCollidablePositionsJob = new HashGridPositionsJob
        {
            gridPositions = dynamicCollidableGridPositions,
            hashMap = dynamicCollidableHashMap.ToConcurrent(),
        };
        var hashDynamicCollidablePositionsJobHandle = hashDynamicCollidablePositionsJob.Schedule(dynamicCollidableCount, 64, inputDeps);

        var movementBarrierHandle = JobHandle.CombineDependencies(hashStaticCollidablePositionsJobHandle, hashDynamicCollidablePositionsJobHandle);

        var moveRandomlyJob = new MoveRandomlyJob
        {
            gridPositions = moveRandomlyGridPositions,
            nextGridPositions = nextGridPositions,
            staticCollidableHashMap = staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            tick = Time.frameCount,
        };
        var moveRandomlyJobHandle = moveRandomlyJob.Schedule(moveRandomlyCount, 64, movementBarrierHandle);

        var hashNextGridPositionsJob = new HashGridPositionsNativeArrayJob
        {
            gridPositions = nextGridPositions,
            hashMap = nextGridPositionsHashMap.ToConcurrent(),
        };
        var hashNextGridPositionsJobHandle = hashNextGridPositionsJob.Schedule(moveRandomlyCount, 64, moveRandomlyJobHandle);

        var resolveCollidedMovementJob = new ResolveCollidedMovementJob
        {
            updatedGridPositions = nextGridPositions,
            gridPositionArray = moveRandomlyGridPositions,
            translationArray = moveRandomlyPositions,
        };
        var resolveCollidedMovementJobHandle = resolveCollidedMovementJob.Schedule(nextGridPositionsHashMap, 64, hashNextGridPositionsJobHandle);

        var writeEntityDataJob = new WriteEntityDataJob
        {
            gridPositionArray = moveRandomlyGridPositions,
            translationArray = moveRandomlyPositions,
        };
        var writeEntityDataJobHandle = writeEntityDataJob.Schedule(m_MoveRandomlyGroup, resolveCollidedMovementJobHandle);

        return writeEntityDataJobHandle;
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

        m_MoveRandomlyGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(MoveRandomly)),
            typeof(GridPosition),
            typeof(Translation)
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.staticCollidableHashMap.IsCreated)
            m_PrevGridState.staticCollidableHashMap.Dispose();
        if (m_PrevGridState.dynamicCollidableGridPositions.IsCreated)
            m_PrevGridState.dynamicCollidableGridPositions.Dispose();
        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
        if (m_PrevGridState.moveRandomlyGridPositions.IsCreated)
            m_PrevGridState.moveRandomlyGridPositions.Dispose();
        if (m_PrevGridState.moveRandomlyTranslations.IsCreated)
            m_PrevGridState.moveRandomlyTranslations.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.nextGridPositionsHashMap.IsCreated)
            m_PrevGridState.nextGridPositionsHashMap.Dispose();
    }
}
