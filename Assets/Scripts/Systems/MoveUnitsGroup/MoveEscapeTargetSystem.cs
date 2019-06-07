using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public class MoveEscapeTargetSystem : JobComponentSystem
{
    private EntityQuery m_LineOfSightGroup;
    private EntityQuery m_MoveEscapeTargetGroup;

    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> unitsLineOfSight;
        public NativeArray<NextGridPosition> nextGridPositions;
        public NativeArray<GridPosition> moveEscapeGridPositions;
        public NativeMultiHashMap<int, int> moveEscapeTargetHashMap;
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
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeArray<NextGridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> targetGridPositionsHashMap;
        public int viewDistance;

        private bool InLineOfSight(int3 initialGridPosition, int3 targetGridPosition)
        {
            // Traverse the grid along the ray from initial position to target position
            int dx = targetGridPosition.x - initialGridPosition.x;
            int dz = targetGridPosition.z - initialGridPosition.z;
            int step;
            if (math.abs(dx) >= math.abs(dz))
                step = math.abs(dx);
            else
                step = math.abs(dz);
            dx /= step;
            dz /= step;
            
            int x = initialGridPosition.x;
            int z = initialGridPosition.z;
            for (int i = 1; i <= step; i++)
            {
                x += dx;
                z += dz;
                int3 gridPosition = new int3(x, initialGridPosition.y, z);
                int key = GridHash.Hash(gridPosition);
                if (staticCollidableHashMap.TryGetFirstValue(key, out _, out _))
                {
                    return false;
                }
                if (dynamicCollidableHashMap.TryGetFirstValue(key, out _, out _))
                {
                    return false;
                }
            }

            return true;
        }

        public void Execute(int index)
        {
            int3 myGridPositionValue = gridPositions[index].Value;

            bool moved = false;

            bool foundTarget = false;
            float3 averageTarget = new int3(0, 0, 0);
            int targetCount = 0;
            for (int checkDist = 1; checkDist <= viewDistance; checkDist++)
            {
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
                                // Check if we have line of sight to the target
                                if (InLineOfSight(myGridPositionValue, targetGridPosition))
                                {
                                    averageTarget = averageTarget * targetCount + new float3(x, 0, z);
                                    targetCount++;
                                    averageTarget /= targetCount;

                                    foundTarget = true;
                                }
                            }
                        }
                    }
                }
            }

            if (foundTarget)
            {
                int3 direction = new int3((int)-averageTarget.x, (int)averageTarget.y, (int)-averageTarget.z);

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

        var unitsLineOfSight = m_LineOfSightGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var unitsLineOfSightCount = unitsLineOfSight.Length;
        var nextGridPositions = m_LineOfSightGroup.ToComponentDataArray<NextGridPosition>(Allocator.TempJob);

        var moveEscapeGridPositions = m_MoveEscapeTargetGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var moveEscapeCount = moveEscapeGridPositions.Length;
        var moveEscapeTargetHashMap = new NativeMultiHashMap<int, int>(moveEscapeCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            unitsLineOfSight = unitsLineOfSight,
            nextGridPositions = nextGridPositions,
            moveEscapeGridPositions = moveEscapeGridPositions,
            moveEscapeTargetHashMap = moveEscapeTargetHashMap,
        };

        if (m_PrevGridState.unitsLineOfSight.IsCreated)
            m_PrevGridState.unitsLineOfSight.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.moveEscapeGridPositions.IsCreated)
            m_PrevGridState.moveEscapeGridPositions.Dispose();
        if (m_PrevGridState.moveEscapeTargetHashMap.IsCreated)
            m_PrevGridState.moveEscapeTargetHashMap.Dispose();
        m_PrevGridState = nextGridState;

        var hashMoveEscapeTargetGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = moveEscapeGridPositions,
            hashMap = moveEscapeTargetHashMap.ToConcurrent(),
        };
        var hashMoveEscapeTargetGridPositionsJobHandle = hashMoveEscapeTargetGridPositionsJob.Schedule(moveEscapeCount, 64, inputDeps);

        var barrier = JobHandle.CombineDependencies(hashMoveEscapeTargetGridPositionsJobHandle, World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableJobHandle, World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableJobHandle);

        var moveAwayFromUnitsJob = new MoveAwayFromUnitsJob
        {
            staticCollidableHashMap = staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            gridPositions = unitsLineOfSight,
            nextGridPositions = nextGridPositions,
            targetGridPositionsHashMap = moveEscapeTargetHashMap,
            viewDistance = GameController.instance.humanVisionDistance,
        };
        var moveAwayFromUnitsJobHandle = moveAwayFromUnitsJob.Schedule(unitsLineOfSightCount, 64, barrier);

        m_LineOfSightGroup.AddDependency(moveAwayFromUnitsJobHandle);
        m_LineOfSightGroup.CopyFromComponentDataArray(nextGridPositions, out JobHandle copyDataJobHandle);

        return copyDataJobHandle;
    }

    protected override void OnCreate()
    {
        m_LineOfSightGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(LineOfSight)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            typeof(NextGridPosition)
        );
        m_MoveEscapeTargetGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(MoveEscapeTarget)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnStopRunning()
    {
        if (m_PrevGridState.unitsLineOfSight.IsCreated)
            m_PrevGridState.unitsLineOfSight.Dispose();
        if (m_PrevGridState.nextGridPositions.IsCreated)
            m_PrevGridState.nextGridPositions.Dispose();
        if (m_PrevGridState.moveEscapeGridPositions.IsCreated)
            m_PrevGridState.moveEscapeGridPositions.Dispose();
        if (m_PrevGridState.moveEscapeTargetHashMap.IsCreated)
            m_PrevGridState.moveEscapeTargetHashMap.Dispose();
    }
}
