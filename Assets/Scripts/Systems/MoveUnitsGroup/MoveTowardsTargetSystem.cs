using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
public class MoveTowardsTargetSystem : JobComponentSystem
{
    private EntityQuery m_MoveTowardsTargetGroup;
    private EntityQuery m_FollowTargetGroup;
    private EntityQuery m_AudibleGroup;

    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> movingUnitsGridPositions;
        public NativeArray<NextGridPosition> nextGridPositions;
        public NativeArray<GridPosition> followTargetGridPositions;
        public NativeMultiHashMap<int, int> followTargetGridPositionsHashMap;
        public NativeArray<Audible> audiblesArray;
        public NativeMultiHashMap<int, int> audiblesHashMap;
        public NativeArray<TurnsUntilMove> turnsUntilMoveArray;
    }

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
    struct HashAudiblesJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<Audible> audiblesArray;
        public NativeMultiHashMap<int, int>.ParallelWriter hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(audiblesArray[index].GridPositionValue);
            hashMap.Add(hash, index);
        }
    }

    [BurstCompile]
    struct MoveTowardsTargetJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeArray<NextGridPosition> nextGridPositions;
        [ReadOnly] public NativeArray<TurnsUntilMove> turnsUntilMoveArray;
        [ReadOnly] public NativeMultiHashMap<int, int> staticCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> targetGridPositionsHashMap;
        [ReadOnly] public NativeArray<Audible> audiblesArray;
        [ReadOnly] public NativeMultiHashMap<int, int> audiblesHashMap;
        public int viewDistance;
        public int hearingDistance;

        public void Execute(int index)
        {
            int3 myGridPositionValue = gridPositions[index].Value;
            if (turnsUntilMoveArray[index].Value != 0)
                return;

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

                                nearestDistance = math.select(nearestDistance, distance, nearest);
                                nearestTarget = math.select(nearestTarget, targetGridPosition, nearest);

                                foundTarget = true;
                            }
                        }
                    }
                }
            }

            // Check for Audible entities in range
            for (int checkDist = 1; checkDist <= hearingDistance && !foundTarget; checkDist++)
            {
                float nearestDistance = 300;
                for (int z = -checkDist; z <= checkDist; z++)
                {
                    for (int x = -checkDist; x <= checkDist; x++)
                    {
                        if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                        {
                            int3 targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);

                            int targetKey = GridHash.Hash(targetGridPosition);
                            if (audiblesHashMap.TryGetFirstValue(targetKey, out int audibleIndex, out _))
                            {
                                var audiblePointingToValue = audiblesArray[audibleIndex].Target;
                                var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(audiblePointingToValue));
                                var nearest = distance < nearestDistance;

                                nearestDistance = math.select(nearestDistance, distance, nearest);
                                nearestTarget = math.select(nearestTarget, audiblePointingToValue, nearest);

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

            nextGridPositions[index] = new NextGridPosition { Value = myGridPositionValue };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var staticCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMap;
        var dynamicCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMap;

        var movingUnitsGridPositions = m_MoveTowardsTargetGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var movingUnitsCount = movingUnitsGridPositions.Length;
        var nextGridPositions = m_MoveTowardsTargetGroup.ToComponentDataArray<NextGridPosition>(Allocator.TempJob);
        var turnsUntilMoveArray = m_MoveTowardsTargetGroup.ToComponentDataArray<TurnsUntilMove>(Allocator.TempJob);

        var followTargetGridPositions = m_FollowTargetGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var followTargetCount = followTargetGridPositions.Length;
        var followTargetGridPositionsHashMap = new NativeMultiHashMap<int, int>(followTargetCount, Allocator.TempJob);

        var audiblesArray = m_AudibleGroup.ToComponentDataArray<Audible>(Allocator.TempJob);
        var audiblesCount = audiblesArray.Length;
        var audiblesHashMap = new NativeMultiHashMap<int, int>(audiblesCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            movingUnitsGridPositions = movingUnitsGridPositions,
            nextGridPositions = nextGridPositions,
            followTargetGridPositions = followTargetGridPositions,
            followTargetGridPositionsHashMap = followTargetGridPositionsHashMap,
            audiblesArray = audiblesArray,
            audiblesHashMap = audiblesHashMap,
            turnsUntilMoveArray = turnsUntilMoveArray,
        };

        if (m_PrevGridState.movingUnitsGridPositions.IsCreated)
            m_PrevGridState.movingUnitsGridPositions.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.followTargetGridPositions.IsCreated)
            m_PrevGridState.followTargetGridPositions.Dispose();
        if (m_PrevGridState.followTargetGridPositionsHashMap.IsCreated)
            m_PrevGridState.followTargetGridPositionsHashMap.Dispose();
        if (m_PrevGridState.audiblesArray.IsCreated)
            m_PrevGridState.audiblesArray.Dispose();
        if (m_PrevGridState.audiblesHashMap.IsCreated)
            m_PrevGridState.audiblesHashMap.Dispose();
        if (m_PrevGridState.turnsUntilMoveArray.IsCreated)
            m_PrevGridState.turnsUntilMoveArray.Dispose();
        m_PrevGridState = nextGridState;

        var hashFollowTargetGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = followTargetGridPositions,
            hashMap = followTargetGridPositionsHashMap.AsParallelWriter(),
        };
        var hashFollowTargetGridPositionsJobHandle = hashFollowTargetGridPositionsJob.Schedule(followTargetCount, 64, inputDeps);

        var hashAudibleGridPositionsJob = new HashAudiblesJob
        {
            audiblesArray = audiblesArray,
            hashMap = audiblesHashMap.AsParallelWriter(),
        };
        var hashAudibleGridPositionsJobHandle = hashAudibleGridPositionsJob.Schedule(audiblesCount, 64, inputDeps);

        var movementBarrierHandle = JobHandle.CombineDependencies(hashFollowTargetGridPositionsJobHandle, hashAudibleGridPositionsJobHandle);

        var moveTowardsTargetJob = new MoveTowardsTargetJob
        {
            gridPositions = movingUnitsGridPositions,
            turnsUntilMoveArray = turnsUntilMoveArray,
            nextGridPositions = nextGridPositions,
            staticCollidableHashMap = staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            targetGridPositionsHashMap = followTargetGridPositionsHashMap,
            audiblesArray = audiblesArray,
            audiblesHashMap = audiblesHashMap,
            viewDistance = GameController.instance.zombieVisionDistance,
            hearingDistance = GameController.instance.zombieHearingDistance,
        };
        var moveTowardsTargetJobHandle = moveTowardsTargetJob.Schedule(movingUnitsCount, 64, movementBarrierHandle);

        m_MoveTowardsTargetGroup.AddDependency(moveTowardsTargetJobHandle);
        m_MoveTowardsTargetGroup.CopyFromComponentDataArray(nextGridPositions, out JobHandle copyDataJobHandle);

        return copyDataJobHandle;
    }

    protected override void OnCreate()
    {
        m_MoveTowardsTargetGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(MoveTowardsTarget)),
            ComponentType.ReadOnly(typeof(TurnsUntilMove)),
            typeof(GridPosition),
            typeof(NextGridPosition)
        );
        m_FollowTargetGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(FollowTarget)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_AudibleGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Audible))
        );
    }

    protected override void OnStopRunning()
    {
        if (m_PrevGridState.movingUnitsGridPositions.IsCreated)
            m_PrevGridState.movingUnitsGridPositions.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.followTargetGridPositions.IsCreated)
            m_PrevGridState.followTargetGridPositions.Dispose();
        if (m_PrevGridState.followTargetGridPositionsHashMap.IsCreated)
            m_PrevGridState.followTargetGridPositionsHashMap.Dispose();
        if (m_PrevGridState.audiblesArray.IsCreated)
            m_PrevGridState.audiblesArray.Dispose();
        if (m_PrevGridState.audiblesHashMap.IsCreated)
            m_PrevGridState.audiblesHashMap.Dispose();
        if (m_PrevGridState.turnsUntilMoveArray.IsCreated)
            m_PrevGridState.turnsUntilMoveArray.Dispose();
    }
}
