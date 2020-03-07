using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public class MoveEscapeTargetSystem : JobComponentSystem
{
    private EntityQuery m_MoveEscapeTargetQuery;
    private NativeHashMap<int, int> m_MoveEscapeTargetHashMap;

    protected override void OnStopRunning()
    {
        if (m_MoveEscapeTargetHashMap.IsCreated)
            m_MoveEscapeTargetHashMap.Dispose();
    }

    private static bool InLineOfSight(int3 initialGridPosition, int3 targetGridPosition, NativeHashMap<int, int> staticCollidableHashMap)
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
            int key = (int)math.hash(gridPosition);
            if (staticCollidableHashMap.TryGetValue(key, out _))
            {
                return false;
            }
        }

        return true;
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        inputDeps = JobHandle.CombineDependencies(inputDeps,
            World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableJobHandle,
            World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableJobHandle
        );

        var staticCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMap;
        var dynamicCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMap;

        if (m_MoveEscapeTargetHashMap.IsCreated)
            m_MoveEscapeTargetHashMap.Dispose();

        var moveEscapeTargetCount = m_MoveEscapeTargetQuery.CalculateEntityCount();
        m_MoveEscapeTargetHashMap = new NativeHashMap<int, int>(moveEscapeTargetCount, Allocator.TempJob);

        var moveEscapeTargetParallelWriter = m_MoveEscapeTargetHashMap.AsParallelWriter();
        var hashFollowTargetGridPositionsJobHandle = Entities
            .WithName("HashMoveEscapeTargetGridPositions")
            .WithAll<MoveEscapeTarget>()
            .WithStoreEntityQueryInField(ref m_MoveEscapeTargetQuery)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
            {
                var hash = (int)math.hash(gridPosition.Value);
                moveEscapeTargetParallelWriter.TryAdd(hash, entityInQueryIndex);
            })
            .Schedule(inputDeps);

        var viewDistance = GameController.instance.humanVisionDistance;
        var moveEscapeTargetHashMap = m_MoveEscapeTargetHashMap;

        var lineOfSightEscapeJob = Entities
            .WithName("LineOfSightEscape")
            .WithAll<LineOfSight>()
            .WithReadOnly(staticCollidableHashMap)
            .WithReadOnly(dynamicCollidableHashMap)
            .WithReadOnly(moveEscapeTargetHashMap)
            .WithBurst()
            .ForEach((ref NextGridPosition nextGridPosition, in TurnsUntilActive turnsUntilActive, in GridPosition gridPosition) =>
            {
                if (turnsUntilActive.Value != 0)
                    return;

                int3 myGridPositionValue = gridPosition.Value;
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
                                int targetKey = (int)math.hash(targetGridPosition);

                                if (moveEscapeTargetHashMap.TryGetValue(targetKey, out _))
                                {
                                    // Check if we have line of sight to the target
                                    if (InLineOfSight(myGridPositionValue, targetGridPosition, staticCollidableHashMap))
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
                    int moveLeftKey = (int)math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
                    int moveRightKey = (int)math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
                    int moveDownKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
                    int moveUpKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
                    if (math.abs(direction.x) >= math.abs(direction.z))
                    {
                        // Move horizontally
                        if (direction.x < 0)
                        {
                            if (!staticCollidableHashMap.TryGetValue(moveLeftKey, out _) &&
                                !dynamicCollidableHashMap.TryGetValue(moveLeftKey, out _))
                            {
                                myGridPositionValue.x--;
                                moved = true;
                            }
                        }
                        else
                        {
                            if (!staticCollidableHashMap.TryGetValue(moveRightKey, out _) &&
                                !dynamicCollidableHashMap.TryGetValue(moveRightKey, out _))
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
                            if (!staticCollidableHashMap.TryGetValue(moveDownKey, out _) &&
                                !dynamicCollidableHashMap.TryGetValue(moveDownKey, out _))
                            {
                                myGridPositionValue.z--;
                                moved = true;
                            }
                        }
                        else
                        {
                            if (!staticCollidableHashMap.TryGetValue(moveUpKey, out _) &&
                                !dynamicCollidableHashMap.TryGetValue(moveUpKey, out _))
                            {
                                myGridPositionValue.z++;
                                moved = true;
                            }
                        }
                    }
                }

                nextGridPosition = new NextGridPosition { Value = myGridPositionValue };
            })
            .Schedule(hashFollowTargetGridPositionsJobHandle);

        return lineOfSightEscapeJob;
    }
}
