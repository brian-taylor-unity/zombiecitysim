using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(MoveRandomlySystem))]
public class MoveFollowTargetSystem : JobComponentSystem
{
    private EntityQuery m_StaticCollidableGroup;
    private EntityQuery m_DynamicCollidableGroup;
    private EntityQuery m_MoveFollowTargetGroup;
    private EntityQuery m_FollowTargetGroup;

    private NativeMultiHashMap<int, int> m_StaticCollidableHashMap;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> dynamicCollidableGridPositions;
        public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        public NativeArray<GridPosition> movingUnitsGridPositions;
        public NativeArray<Translation> movingUnitsTranslations;
        public NativeArray<NextGridPosition> nextGridPositions;
        public NativeMultiHashMap<int, int> nextGridPositionsHashMap;
        public NativeArray<GridPosition> followTargetGridPositions;
        public NativeMultiHashMap<int, int> followTargetGridPositionsHashMap;
        public NativeArray<TurnsUntilMove> turnsUntilMoveArray;
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
    struct MoveFollowTargetJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        [ReadOnly] public NativeArray<TurnsUntilMove> turnsUntilMoveArray;
        public NativeArray<NextGridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> staticCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> targetGridPositionsHashMap;
        public int viewDistance;

        public void Execute(int index)
        {
            int3 myGridPositionValue = gridPositions[index].Value;
            if (turnsUntilMoveArray[index].Value != 0)
                return;

            bool moved = false;

            // Get nearest target
            // Check all grid positions that are checkDist away in the x or y direction
            bool foundTarget = false;
            int3 nearestTarget = myGridPositionValue;
            for (int checkDist = 1; checkDist <= viewDistance; checkDist++)
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

                if (foundTarget)
                    break;
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

            nextGridPositions[index] = new NextGridPosition { Value = myGridPositionValue };
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

        var movingUnitsGridPositions = m_MoveFollowTargetGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var movingUnitsTranslations = m_MoveFollowTargetGroup.ToComponentDataArray<Translation>(Allocator.TempJob);
        var movingUnitsCount = movingUnitsGridPositions.Length;
        var nextGridPositions = m_MoveFollowTargetGroup.ToComponentDataArray<NextGridPosition>(Allocator.TempJob);
        var nextGridPositionsHashMap = new NativeMultiHashMap<int, int>(movingUnitsCount, Allocator.TempJob);
        var turnsUntilMoveArray = m_MoveFollowTargetGroup.ToComponentDataArray<TurnsUntilMove>(Allocator.TempJob);

        var followTargetGridPositions = m_FollowTargetGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var followTargetCount = followTargetGridPositions.Length;
        var followTargetGridPositionsHashMap = new NativeMultiHashMap<int, int>(followTargetCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            dynamicCollidableGridPositions = dynamicCollidableGridPositions,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            movingUnitsGridPositions = movingUnitsGridPositions,
            movingUnitsTranslations = movingUnitsTranslations,
            nextGridPositions = nextGridPositions,
            nextGridPositionsHashMap = nextGridPositionsHashMap,
            followTargetGridPositions = followTargetGridPositions,
            followTargetGridPositionsHashMap = followTargetGridPositionsHashMap,
            turnsUntilMoveArray = turnsUntilMoveArray,
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
        if (m_PrevGridState.dynamicCollidableGridPositions.IsCreated)
            m_PrevGridState.dynamicCollidableGridPositions.Dispose();
        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
        if (m_PrevGridState.movingUnitsGridPositions.IsCreated)
            m_PrevGridState.movingUnitsGridPositions.Dispose();
        if (m_PrevGridState.movingUnitsTranslations.IsCreated)
            m_PrevGridState.movingUnitsTranslations.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.nextGridPositionsHashMap.IsCreated)
            m_PrevGridState.nextGridPositionsHashMap.Dispose();
        if (m_PrevGridState.followTargetGridPositions.IsCreated)
            m_PrevGridState.followTargetGridPositions.Dispose();
        if (m_PrevGridState.followTargetGridPositionsHashMap.IsCreated)
            m_PrevGridState.followTargetGridPositionsHashMap.Dispose();
        if (m_PrevGridState.turnsUntilMoveArray.IsCreated)
            m_PrevGridState.turnsUntilMoveArray.Dispose();
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

        var movementBarrierHandle = JobHandle.CombineDependencies(hashStaticCollidablePositionsJobHandle, hashDynamicCollidableGridPositionsJobHandle, hashFollowTargetGridPositionsJobHandle);

        var moveFollowTargetJob = new MoveFollowTargetJob
        {
            gridPositions = movingUnitsGridPositions,
            turnsUntilMoveArray = turnsUntilMoveArray,
            nextGridPositions = nextGridPositions,
            staticCollidableHashMap = m_StaticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            targetGridPositionsHashMap = followTargetGridPositionsHashMap,
            viewDistance = GameController.instance.zombieVisionDistance,
        };
        var moveFollowTargetJobHandle = moveFollowTargetJob.Schedule(movingUnitsCount, 64, movementBarrierHandle);

        m_MoveFollowTargetGroup.AddDependency(moveFollowTargetJobHandle);
        m_MoveFollowTargetGroup.CopyFromComponentDataArray(nextGridPositions, out JobHandle copyDataJobHandle);

        return copyDataJobHandle;
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

        m_MoveFollowTargetGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(MoveFollowTarget)),
            ComponentType.ReadOnly(typeof(TurnsUntilMove)),
            typeof(GridPosition),
            typeof(NextGridPosition),
            typeof(Translation)
        );
        m_FollowTargetGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(FollowTarget)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnStopRunning()
    {
        if (m_StaticCollidableHashMap.IsCreated)
            m_StaticCollidableHashMap.Dispose();
        if (m_PrevGridState.dynamicCollidableGridPositions.IsCreated)
            m_PrevGridState.dynamicCollidableGridPositions.Dispose();
        if (m_PrevGridState.dynamicCollidableHashMap.IsCreated)
            m_PrevGridState.dynamicCollidableHashMap.Dispose();
        if (m_PrevGridState.movingUnitsGridPositions.IsCreated)
            m_PrevGridState.movingUnitsGridPositions.Dispose();
        if (m_PrevGridState.movingUnitsTranslations.IsCreated)
            m_PrevGridState.movingUnitsTranslations.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.nextGridPositionsHashMap.IsCreated)
            m_PrevGridState.nextGridPositionsHashMap.Dispose();
        if (m_PrevGridState.followTargetGridPositions.IsCreated)
            m_PrevGridState.followTargetGridPositions.Dispose();
        if (m_PrevGridState.followTargetGridPositionsHashMap.IsCreated)
            m_PrevGridState.followTargetGridPositionsHashMap.Dispose();
        if (m_PrevGridState.turnsUntilMoveArray.IsCreated)
            m_PrevGridState.turnsUntilMoveArray.Dispose();
    }
}
