using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public class MoveEscapeTargetSystem : SystemBase
{
    private EntityQuery m_MoveEscapeTargetQuery;

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

    protected override void OnUpdate()
    {
        Dependency = JobHandle.CombineDependencies(
            Dependency,
            World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMapJobHandle,
            World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMapJobHandle
        );

        var staticCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMap;
        var dynamicCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMap;

        var moveEscapeTargetCount = m_MoveEscapeTargetQuery.CalculateEntityCount();
        var moveEscapeTargetHashMap = new NativeHashMap<int, int>(moveEscapeTargetCount, Allocator.TempJob);
        var moveEscapeTargetParallelWriter = moveEscapeTargetHashMap.AsParallelWriter();
        var viewDistance = GameController.instance.humanVisionDistance;
        // We need either "(X * Y) / visionDistance" or "numUnitsToEscapeFrom" hash buckets, whichever is smaller
        var humanVisionHashMap = new NativeHashMap<int, int>(moveEscapeTargetCount, Allocator.TempJob);
        var humanVisionParallelWriter = humanVisionHashMap.AsParallelWriter();

        var hashMoveEscapeTargetGridPositionsJobHandle = Entities
            .WithName("HashMoveEscapeTargetGridPositions")
            .WithAll<MoveEscapeTarget>()
            .WithStoreEntityQueryInField(ref m_MoveEscapeTargetQuery)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
            {
                var hash = (int)math.hash(gridPosition.Value);
                moveEscapeTargetParallelWriter.TryAdd(hash, entityInQueryIndex);
            })
            .ScheduleParallel(Dependency);

        var hashMoveEscapeTargetVisionJobHandle = Entities
            .WithName("HashMoveEscapeTargetVision")
            .WithAll<MoveEscapeTarget>()
            .WithStoreEntityQueryInField(ref m_MoveEscapeTargetQuery)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
            {
                var hash = (int)math.hash(gridPosition.Value / viewDistance);
                humanVisionParallelWriter.TryAdd(hash, entityInQueryIndex);
            })
            .ScheduleParallel(Dependency);

        var movementBarrierHandle = JobHandle.CombineDependencies(
            Dependency,
            hashMoveEscapeTargetGridPositionsJobHandle,
            hashMoveEscapeTargetVisionJobHandle
        );

        var moveEscapeTargetsJobHandle = Entities
            .WithName("MoveEscapeTargets")
            .WithAll<LineOfSight>()
            .WithReadOnly(staticCollidableHashMap)
            .WithReadOnly(dynamicCollidableHashMap)
            .WithReadOnly(moveEscapeTargetHashMap)
            .WithReadOnly(humanVisionHashMap)
            .WithDisposeOnCompletion(moveEscapeTargetHashMap)
            .WithDisposeOnCompletion(humanVisionHashMap)
            .WithBurst()
            .ForEach((ref NextGridPosition nextGridPosition, in TurnsUntilActive turnsUntilActive, in GridPosition gridPosition) =>
            {
                if (turnsUntilActive.Value != 0)
                    return;

                int3 myGridPositionValue = gridPosition.Value;
                float3 averageTarget = new int3(0, 0, 0);
                bool moved = false;
                bool foundTarget = humanVisionHashMap.TryGetValue((int)math.hash(myGridPositionValue / viewDistance), out _) ||
                                   humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - viewDistance, myGridPositionValue.y, myGridPositionValue.z - viewDistance) / viewDistance), out _) ||
                                   humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + viewDistance, myGridPositionValue.y, myGridPositionValue.z - viewDistance) / viewDistance), out _) ||
                                   humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - viewDistance, myGridPositionValue.y, myGridPositionValue.z + viewDistance) / viewDistance), out _) ||
                                   humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + viewDistance, myGridPositionValue.y, myGridPositionValue.z + viewDistance) / viewDistance), out _);

                if (foundTarget)
                {
                    foundTarget = false;

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
            .ScheduleParallel(movementBarrierHandle);

        Dependency = moveEscapeTargetsJobHandle;
    }
}
