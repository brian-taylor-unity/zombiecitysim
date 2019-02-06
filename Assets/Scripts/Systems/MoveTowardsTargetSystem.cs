using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(MoveRandomlySystem))]
public class MoveTowardsTargetSystem : JobComponentSystem
{
    private ComponentGroup m_StaticCollidableGroup;
    private ComponentGroup m_DynamicCollidableGroup;
    private ComponentGroup m_MoveFollowTargetGroup;
    private ComponentGroup m_FollowTargetGroup;
    private ComponentGroup m_AudibleGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> staticCollidableHashMap;
        public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        public NativeMultiHashMap<int, int> nextGridPositionsHashMap;
        public NativeMultiHashMap<int, int> followTargetGridPositionsHashMap;
        public NativeMultiHashMap<int, int> audibleGridPositionsHashMap;
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
    struct MoveTowardsTargetJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> gridPositions;
        public NativeArray<GridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> staticCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> targetGridPositionsHashMap;
        [ReadOnly] public ComponentDataArray<GridPosition> audibleGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> audibleGridPositionsHashMap;
        public int viewDistance;
        public int hearingDistance;

        public void Execute(int index)
        {
            int3 myGridPositionValue = gridPositions[index].Value;
            bool moved = false;

            // Get nearest visible target
            // Check all grid positions that are checkDist away in the x or y direction
            bool foundTarget = false;
            int3 nearestTarget = myGridPositionValue;
            for (int checkDist = 1; checkDist <= viewDistance && !foundTarget; checkDist++)
            {
                float nearestDistance = (checkDist + 1) * (checkDist + 1);
                for (int z = -checkDist; z <= checkDist; z++)
                {
                    for (int x = -checkDist; x <= checkDist; x++)
                    {
                        if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                        {
                            int3 targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);

                            int targetKey = GridHash.Hash(targetGridPosition);
                            if (targetGridPositionsHashMap.TryGetFirstValue(targetKey, out _, out _))
                            {
                                var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                                var nearest = distance < nearestDistance;

                                nearestDistance = math.@select(nearestDistance, distance, nearest);
                                nearestTarget = math.@select(nearestTarget, targetGridPosition, nearest);

                                foundTarget = true;
                            }
                        }
                    }
                }
            }

            // Check for Audible entities in range
            for (int checkDist = 1; checkDist <= hearingDistance && !foundTarget; checkDist++)
            {
                float nearestDistance = (checkDist + 1) * (checkDist + 1);
                for (int z = -checkDist; z <= checkDist; z++)
                {
                    for (int x = -checkDist; x <= checkDist; x++)
                    {
                        if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                        {
                            int3 targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);

                            int targetKey = GridHash.Hash(targetGridPosition);
                            if (audibleGridPositionsHashMap.TryGetFirstValue(targetKey, out int audibleIndex, out _))
                            {
                                var audiblePointingToValue = audibleGridPositions[audibleIndex].Value;
                                var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(audiblePointingToValue));
                                var nearest = distance < nearestDistance;

                                nearestDistance = math.@select(nearestDistance, distance, nearest);
                                nearestTarget = math.@select(nearestTarget, audiblePointingToValue, nearest);

                                foundTarget = true;
                            }
                        }
                    }
                }
            }

            if (foundTarget)
            {
                int3 direction = nearestTarget - myGridPositionValue;

                // Check if space is already occupied
                int moveLeftKey = GridHash.Hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
                int moveRightKey = GridHash.Hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
                int moveDownKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
                int moveUpKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
                if (math.abs(direction.x) >= math.abs(direction.z))
                {
                    // Move horizontally
                    if (direction.x < 0)
                    {
                        if (!staticCollidableHashMap.TryGetFirstValue(moveLeftKey, out _, out _) &&
                            !dynamicCollidableHashMap.TryGetFirstValue(moveLeftKey, out _, out _))
                        {
                            myGridPositionValue.x--;
                            moved = true;
                        }
                    }
                    else if (direction.x > 0)
                    {
                        if (!staticCollidableHashMap.TryGetFirstValue(moveRightKey, out _, out _) &&
                            !dynamicCollidableHashMap.TryGetFirstValue(moveRightKey, out _, out _))
                        {
                            myGridPositionValue.x++;
                            moved = true;
                        }
                    }
                }
                // Unit maybe wanted to move horizontally but couldn't, so check if it wants to move vertically
                if (!moved)
                {
                    // Move vertically
                    if (direction.z < 0)
                    {
                        if (!staticCollidableHashMap.TryGetFirstValue(moveDownKey, out _, out _) &&
                            !dynamicCollidableHashMap.TryGetFirstValue(moveDownKey, out _, out _))
                        {
                            myGridPositionValue.z--;
                            moved = true;
                        }
                    }
                    else if (direction.z > 0)
                    {
                        if (!staticCollidableHashMap.TryGetFirstValue(moveUpKey, out _, out _) &&
                            !dynamicCollidableHashMap.TryGetFirstValue(moveUpKey, out _, out _))
                        {
                            myGridPositionValue.z++;
                            moved = true;
                        }
                    }
                }
            }

            nextGridPositions[index] = new GridPosition { Value = myGridPositionValue };
        }
    }

    [BurstCompile]
    struct ResolveCollidedMovementJob : IJobNativeMultiHashMapMergedSharedKeyIndices
    {
        [ReadOnly] public NativeArray<GridPosition> nextGridPositions;
        public ComponentDataArray<GridPosition> gridPositionComponentData;
        public ComponentDataArray<Position> positionComponentData;

        public void ExecuteFirst(int index)
        {
            // This was the first unit added
            int seed = GridHash.Hash(nextGridPositions[index].Value);
            gridPositionComponentData[index] = nextGridPositions[index];
            positionComponentData[index] = new Position { Value = new float3(nextGridPositions[index].Value) };
        }

        public void ExecuteNext(int innerIndex, int index)
        {
            // Don't move this unit
        }
    }

    [BurstCompile]
    struct DeallocateJob : IJob
    {
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<GridPosition> gridPositionsArray;

        public void Execute()
        {
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        NativeMultiHashMap<int, int> staticCollidableHashMap;

        var dynamicCollidableGridPositions = m_DynamicCollidableGroup.GetComponentDataArray<GridPosition>();
        var dynamicCollidableCount = dynamicCollidableGridPositions.Length;
        var dynamicCollidableHashMap = new NativeMultiHashMap<int, int>(dynamicCollidableCount, Allocator.TempJob);

        var movingUnitsGridPositions = m_MoveFollowTargetGroup.GetComponentDataArray<GridPosition>();
        var movingUnitsPositions = m_MoveFollowTargetGroup.GetComponentDataArray<Position>();
        var movingUnitsCount = movingUnitsGridPositions.Length;
        var nextGridPositions = new NativeArray<GridPosition>(movingUnitsCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var nextGridPositionsHashMap = new NativeMultiHashMap<int, int>(movingUnitsCount, Allocator.TempJob);

        var followTargetGridPositions = m_FollowTargetGroup.GetComponentDataArray<GridPosition>();
        var followTargetCount = followTargetGridPositions.Length;
        var followTargetGridPositionsHashMap = new NativeMultiHashMap<int, int>(followTargetCount, Allocator.TempJob);

        var audibleGridPositions = m_AudibleGroup.GetComponentDataArray<GridPosition>();
        var audibleCount = audibleGridPositions.Length;
        var audibleGridPositionsHashMap = new NativeMultiHashMap<int, int>(audibleCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            staticCollidableHashMap = m_PrevGridState.staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            nextGridPositionsHashMap = nextGridPositionsHashMap,
            followTargetGridPositionsHashMap = followTargetGridPositionsHashMap,
            audibleGridPositionsHashMap = audibleGridPositionsHashMap,
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
        if (m_PrevGridState.nextGridPositionsHashMap.IsCreated)
            m_PrevGridState.nextGridPositionsHashMap.Dispose();
        if (m_PrevGridState.followTargetGridPositionsHashMap.IsCreated)
            m_PrevGridState.followTargetGridPositionsHashMap.Dispose();
        if (m_PrevGridState.audibleGridPositionsHashMap.IsCreated)
            m_PrevGridState.audibleGridPositionsHashMap.Dispose();

        m_PrevGridState = nextGridState;

        var hashDynamicCollidableGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = dynamicCollidableGridPositions,
            hashMap = dynamicCollidableHashMap.ToConcurrent(),
        };
        var hashDynamicCollidableGridPositionsJobHandle = hashDynamicCollidableGridPositionsJob.Schedule(dynamicCollidableCount, 64, inputDeps);

        var hashFollowTargetGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = followTargetGridPositions,
            hashMap = followTargetGridPositionsHashMap.ToConcurrent(),
        };
        var hashFollowTargetGridPositionsJobHandle = hashFollowTargetGridPositionsJob.Schedule(followTargetCount, 64, inputDeps);

        var hashAudibleGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = audibleGridPositions,
            hashMap = audibleGridPositionsHashMap.ToConcurrent(),
        };
        var hashAudibleGridPositionsJobHandle = hashAudibleGridPositionsJob.Schedule(audibleCount, 64, inputDeps);

        var movementBarrierHandle = JobHandle.CombineDependencies(hashStaticCollidablePositionsJobHandle, hashDynamicCollidableGridPositionsJobHandle, hashFollowTargetGridPositionsJobHandle);
        movementBarrierHandle = JobHandle.CombineDependencies(movementBarrierHandle, hashAudibleGridPositionsJobHandle);

        var moveTowardsTargetJob = new MoveTowardsTargetJob
        {
            gridPositions = movingUnitsGridPositions,
            nextGridPositions = nextGridPositions,
            staticCollidableHashMap = staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            targetGridPositionsHashMap = followTargetGridPositionsHashMap,
            audibleGridPositions = audibleGridPositions,
            audibleGridPositionsHashMap = audibleGridPositionsHashMap,
            viewDistance = Bootstrap.ZombieVisionDistance,
            hearingDistance = Bootstrap.ZombieHearingDistance,
        };
        var moveTowardsTargetJobHandle = moveTowardsTargetJob.Schedule(movingUnitsCount, 64, movementBarrierHandle);

        var hashNextGridPositionsJob = new HashGridPositionsNativeArrayJob
        {
            gridPositions = nextGridPositions,
            hashMap = nextGridPositionsHashMap.ToConcurrent(),
        };
        var hashNextGridPositionsJobHandle = hashNextGridPositionsJob.Schedule(movingUnitsCount, 64, moveTowardsTargetJobHandle);

        var resolveCollidedMovementJob = new ResolveCollidedMovementJob
        {
            nextGridPositions = nextGridPositions,
            gridPositionComponentData = movingUnitsGridPositions,
            positionComponentData = movingUnitsPositions,
        };
        var resolveCollidedMovementJobHandle = resolveCollidedMovementJob.Schedule(nextGridPositionsHashMap, 64, hashNextGridPositionsJobHandle);

        var deallocateJob = new DeallocateJob
        {
            gridPositionsArray = nextGridPositions,
        };
        var deallocateJobHandle = deallocateJob.Schedule(resolveCollidedMovementJobHandle);

        return deallocateJobHandle;
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

        m_MoveFollowTargetGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(MoveTowardsTarget)),
            typeof(GridPosition),
            typeof(Position)
        );
        m_FollowTargetGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(FollowTarget)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_AudibleGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Audible)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.staticCollidableHashMap.IsCreated)
            m_PrevGridState.staticCollidableHashMap.Dispose();
        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
        if (m_PrevGridState.nextGridPositionsHashMap.IsCreated)
            m_PrevGridState.nextGridPositionsHashMap.Dispose();
        if (m_PrevGridState.followTargetGridPositionsHashMap.IsCreated)
            m_PrevGridState.followTargetGridPositionsHashMap.Dispose();
        if (m_PrevGridState.audibleGridPositionsHashMap.IsCreated)
            m_PrevGridState.audibleGridPositionsHashMap.Dispose();
    }
}
