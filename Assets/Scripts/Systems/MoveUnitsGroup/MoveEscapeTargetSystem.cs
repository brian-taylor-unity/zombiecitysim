using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct MoveEscapeTargetJob : IJobEntity
{
    public int visionDistance;
    [ReadOnly] public NativeParallelHashMap<int, int> humanVisionHashMap;

    [ReadOnly] public NativeParallelHashMap<int, int> moveEscapeTargetHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> staticCollidablesHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> dynamicCollidablesHashMap;

    public void Execute(ref NextGridPosition nextGridPosition, in GridPosition gridPosition, in TurnActive turnActive, in LineOfSight lineOfSight)
    {
        var humanVisionHashMapCellSize = visionDistance * 2 + 1;

        int3 myGridPositionValue = gridPosition.Value;
        float3 averageTarget = new int3(0, 0, 0);
        bool moved = false;

        bool foundTarget = humanVisionHashMap.TryGetValue((int)math.hash(myGridPositionValue / humanVisionHashMapCellSize), out _) ||
                           humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - visionDistance, myGridPositionValue.y, myGridPositionValue.z - visionDistance) / humanVisionHashMapCellSize), out _) ||
                           humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + visionDistance, myGridPositionValue.y, myGridPositionValue.z - visionDistance) / humanVisionHashMapCellSize), out _) ||
                           humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - visionDistance, myGridPositionValue.y, myGridPositionValue.z + visionDistance) / humanVisionHashMapCellSize), out _) ||
                           humanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + visionDistance, myGridPositionValue.y, myGridPositionValue.z + visionDistance) / humanVisionHashMapCellSize), out _);

        if (foundTarget)
        {
            foundTarget = false;

            int targetCount = 0;
            for (int checkDist = 1; checkDist <= visionDistance; checkDist++)
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
                                if (LineOfSightUtilities.InLineOfSight(myGridPositionValue, targetGridPosition, staticCollidablesHashMap))
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
                    if (!staticCollidablesHashMap.TryGetValue(moveLeftKey, out _) &&
                        !dynamicCollidablesHashMap.TryGetValue(moveLeftKey, out _))
                    {
                        myGridPositionValue.x--;
                        moved = true;
                    }
                }
                else
                {
                    if (!staticCollidablesHashMap.TryGetValue(moveRightKey, out _) &&
                        !dynamicCollidablesHashMap.TryGetValue(moveRightKey, out _))
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
                    if (!staticCollidablesHashMap.TryGetValue(moveDownKey, out _) &&
                        !dynamicCollidablesHashMap.TryGetValue(moveDownKey, out _))
                    {
                        myGridPositionValue.z--;
                    }
                }
                else
                {
                    if (!staticCollidablesHashMap.TryGetValue(moveUpKey, out _) &&
                        !dynamicCollidablesHashMap.TryGetValue(moveUpKey, out _))
                    {
                        myGridPositionValue.z++;
                    }
                }
            }
        }

        nextGridPosition = new NextGridPosition { Value = myGridPositionValue };
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public partial struct MoveEscapeTargetSystem : ISystem
{
    private EntityQuery _moveEscapeTargetQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _moveEscapeTargetQuery = state.GetEntityQuery(ComponentType.ReadOnly<MoveEscapeTarget>());

        state.RequireForUpdate<StaticCollidableComponent>();
        state.RequireForUpdate<DynamicCollidableComponent>();
        state.RequireForUpdate<GameControllerComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var staticCollidableComponent = SystemAPI.GetSingleton<StaticCollidableComponent>();
        var dynamicCollidableComponent = SystemAPI.GetSingleton<DynamicCollidableComponent>();
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        state.Dependency = JobHandle.CombineDependencies(
            state.Dependency,
            SystemAPI.GetSingleton<StaticCollidableComponent>().Handle,
            SystemAPI.GetSingleton<DynamicCollidableComponent>().Handle
        );

        var staticCollidableHashMap = staticCollidableComponent.HashMap;
        var dynamicCollidableHashMap = dynamicCollidableComponent.HashMap;

        if (!staticCollidableHashMap.IsCreated || !dynamicCollidableHashMap.IsCreated)
            return;

        var moveEscapeTargetCount = _moveEscapeTargetQuery.CalculateEntityCount();
        var moveEscapeTargetHashMap = new NativeParallelHashMap<int, int>(moveEscapeTargetCount, Allocator.TempJob);
        var moveEscapeTargetParallelWriter = moveEscapeTargetHashMap.AsParallelWriter();
        // We need either "(X * Y) / visionDistance" or "numUnitsToEscapeFrom" hash buckets, whichever is smaller
        var humanVisionHashMap = new NativeParallelHashMap<int, int>(moveEscapeTargetCount, Allocator.TempJob);
        var humanVisionParallelWriter = humanVisionHashMap.AsParallelWriter();

        var hashMoveEscapeTargetGridPositionsJobHandle = new HashGridPositionsJob { parallelWriter = moveEscapeTargetParallelWriter }.ScheduleParallel(_moveEscapeTargetQuery, state.Dependency);
        var hashMoveEscapeTargetVisionJobHandle = new HashGridPositionsCellJob { cellSize = gameControllerComponent.humanVisionDistance * 2 + 1, parallelWriter = humanVisionParallelWriter }.ScheduleParallel(_moveEscapeTargetQuery, state.Dependency);

        state.Dependency = JobHandle.CombineDependencies(
            state.Dependency,
            hashMoveEscapeTargetGridPositionsJobHandle,
            hashMoveEscapeTargetVisionJobHandle
        );

        state.Dependency = new MoveEscapeTargetJob
        {
            visionDistance = gameControllerComponent.humanVisionDistance,
            humanVisionHashMap = humanVisionHashMap,

            moveEscapeTargetHashMap = moveEscapeTargetHashMap,
            staticCollidablesHashMap = staticCollidableHashMap,
            dynamicCollidablesHashMap = dynamicCollidableHashMap
        }.ScheduleParallel(state.Dependency);

        moveEscapeTargetHashMap.Dispose(state.Dependency);
        humanVisionHashMap.Dispose(state.Dependency);
    }
}
