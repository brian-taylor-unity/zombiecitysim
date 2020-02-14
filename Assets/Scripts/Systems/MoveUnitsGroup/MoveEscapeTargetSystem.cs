using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public class MoveEscapeTargetSystem : JobComponentSystem
{
    EntityQuery query;
    NativeHashMap<int, int> m_MoveEscapeTargetHashMap;

    protected override void OnStopRunning()
    {
        if (m_MoveEscapeTargetHashMap.IsCreated)
            m_MoveEscapeTargetHashMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var staticCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMap;
        var dynamicCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMap;

        if (m_MoveEscapeTargetHashMap.IsCreated)
            m_MoveEscapeTargetHashMap.Dispose();

        var entityCount = query.CalculateEntityCount();
        if (entityCount == 0)
            return inputDeps;

        m_MoveEscapeTargetHashMap = new NativeHashMap<int, int>(entityCount, Allocator.TempJob);

        var hashMap = m_MoveEscapeTargetHashMap;
        var parallelWriter = m_MoveEscapeTargetHashMap.AsParallelWriter();

        var hashMoveEscapeTargetGridPositionsJobHandle = Entities
            .WithAll<MoveEscapeTarget>()
            .WithStoreEntityQueryInField(ref query)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    parallelWriter.TryAdd(hash, entityInQueryIndex);
                })
            .Schedule(inputDeps);

        var barrier = JobHandle.CombineDependencies(hashMoveEscapeTargetGridPositionsJobHandle, World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableJobHandle, World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableJobHandle);

        var viewDistance = GameController.instance.humanVisionDistance;

        var moveAwayFromUnitsJobHandle = Entities
            .WithAll<MoveEscape>()
            .WithReadOnly(hashMap)
            .WithReadOnly(staticCollidableHashMap)
            .WithReadOnly(dynamicCollidableHashMap)
            .ForEach((ref NextGridPosition nextGridPosition, in GridPosition gridPosition) => 
                {
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

                                    if (hashMap.TryGetValue(targetKey, out _))
                                    {
                                        // Check if we have line of sight to the target
                                        bool lineOfSight = true;

                                        // Traverse the grid along the ray from initial position to target position
                                        int dx = targetGridPosition.x - myGridPositionValue.x;
                                        int dz = targetGridPosition.z - myGridPositionValue.z;
                                        int step;
                                        if (math.abs(dx) >= math.abs(dz))
                                            step = math.abs(dx);
                                        else
                                            step = math.abs(dz);
                                        dx /= step;
                                        dz /= step;

                                        int stepX = myGridPositionValue.x;
                                        int stepZ = myGridPositionValue.z;
                                        for (int i = 1; i <= step; i++)
                                        {
                                            stepX += dx;
                                            stepZ += dz;
                                            int3 stepGridPosition = new int3(stepX, myGridPositionValue.y, stepZ);
                                            int key = (int)math.hash(stepGridPosition);
                                            if (staticCollidableHashMap.TryGetFirstValue(key, out _, out _))
                                            {
                                                lineOfSight = false;
                                            }
                                            if (dynamicCollidableHashMap.TryGetFirstValue(key, out _, out _))
                                            {
                                                lineOfSight = false;
                                            }
                                        }

                                        if (lineOfSight)
                                        {
                                            averageTarget = averageTarget * targetCount + new float3(stepX, 0, stepZ);
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

                    nextGridPosition.Value = myGridPositionValue;
                })
            .Schedule(barrier);

        return moveAwayFromUnitsJobHandle;
    }
}
