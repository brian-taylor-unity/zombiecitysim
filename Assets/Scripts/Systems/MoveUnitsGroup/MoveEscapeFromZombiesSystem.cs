using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct MoveEscapeFromZombiesJob : IJobEntity
{
    public int VisionDistance;
    [ReadOnly] public NativeParallelHashMap<int, int> HumanVisionHashMap;

    [ReadOnly] public NativeParallelHashMap<int, int> ZombieHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> StaticCollidablesHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> DynamicCollidablesHashMap;

    public void Execute(ref NextGridPosition nextGridPosition, in GridPosition gridPosition, in TurnActive turnActive)
    {
        var humanVisionHashMapCellSize = VisionDistance * 2 + 1;

        var myGridPositionValue = gridPosition.Value;
        var averageTarget = new float3(0, 0, 0);
        var moved = false;

        var foundTarget = HumanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / humanVisionHashMapCellSize), out _) ||
                          HumanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / humanVisionHashMapCellSize), out _) ||
                          HumanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / humanVisionHashMapCellSize), out _) ||
                          HumanVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / humanVisionHashMapCellSize), out _);

        if (foundTarget)
        {
            foundTarget = false;

            var targetCount = 0;
            for (var checkDist = 1; checkDist <= VisionDistance; checkDist++)
            {
                for (var z = -checkDist; z <= checkDist; z++)
                {
                    for (var x = -checkDist; x <= checkDist; x++)
                    {
                        if (math.abs(x) != checkDist && math.abs(z) != checkDist)
                            continue;

                        var targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);
                        var targetKey = (int)math.hash(targetGridPosition);

                        if (!ZombieHashMap.TryGetValue(targetKey, out _))
                            continue;

                        // Check if we have line of sight to the target
                        if (!LineOfSightUtilities.InLineOfSight(myGridPositionValue, targetGridPosition, StaticCollidablesHashMap))
                            continue;

                        averageTarget = averageTarget * targetCount + new float3(x, 0, z);
                        targetCount++;
                        averageTarget /= targetCount;

                        foundTarget = true;
                    }
                }
            }
        }

        if (foundTarget)
        {
            var direction = new int3((int)-averageTarget.x, (int)averageTarget.y, (int)-averageTarget.z);

            // Check if space is already occupied
            var moveLeftKey = (int)math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
            var moveRightKey = (int)math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
            var moveDownKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
            var moveUpKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
            if (math.abs(direction.x) >= math.abs(direction.z))
            {
                // Move horizontally
                if (direction.x < 0)
                {
                    if (!StaticCollidablesHashMap.TryGetValue(moveLeftKey, out _) &&
                        !DynamicCollidablesHashMap.TryGetValue(moveLeftKey, out _))
                    {
                        myGridPositionValue.x--;
                        moved = true;
                    }
                }
                else
                {
                    if (!StaticCollidablesHashMap.TryGetValue(moveRightKey, out _) &&
                        !DynamicCollidablesHashMap.TryGetValue(moveRightKey, out _))
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
                    if (!StaticCollidablesHashMap.TryGetValue(moveDownKey, out _) &&
                        !DynamicCollidablesHashMap.TryGetValue(moveDownKey, out _))
                    {
                        myGridPositionValue.z--;
                    }
                }
                else
                {
                    if (!StaticCollidablesHashMap.TryGetValue(moveUpKey, out _) &&
                        !DynamicCollidablesHashMap.TryGetValue(moveUpKey, out _))
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
[UpdateBefore(typeof(MoveTowardsHumansSystem))]
[RequireMatchingQueriesForUpdate]
public partial struct MoveEscapeFromZombiesSystem : ISystem
{
    private EntityQuery _zombieQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _zombieQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Zombie, GridPosition>());

        state.RequireForUpdate<HashStaticCollidableSystemComponent>();
        state.RequireForUpdate<HashDynamicCollidableSystemComponent>();
        state.RequireForUpdate<GameControllerComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var staticCollidableComponent = SystemAPI.GetSingleton<HashStaticCollidableSystemComponent>();
        var dynamicCollidableComponent = SystemAPI.GetSingleton<HashDynamicCollidableSystemComponent>();
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        state.Dependency = JobHandle.CombineDependencies(
            state.Dependency,
            SystemAPI.GetSingleton<HashStaticCollidableSystemComponent>().Handle,
            SystemAPI.GetSingleton<HashDynamicCollidableSystemComponent>().Handle
        );

        var staticCollidableHashMap = staticCollidableComponent.HashMap;
        var dynamicCollidableHashMap = dynamicCollidableComponent.HashMap;

        if (!staticCollidableHashMap.IsCreated || !dynamicCollidableHashMap.IsCreated)
            return;

        var zombieCount = _zombieQuery.CalculateEntityCount();
        var zombieHashMap = new NativeParallelHashMap<int, int>(zombieCount, Allocator.TempJob);
        var hashMoveEscapeTargetGridPositionsJobHandle = new HashGridPositionsJob { ParallelWriter = zombieHashMap.AsParallelWriter() }.ScheduleParallel(_zombieQuery, state.Dependency);

        var cellSize = gameControllerComponent.humanVisionDistance * 2 + 1;
        var cellCount = (gameControllerComponent.numTilesX / cellSize + 1) * (gameControllerComponent.numTilesY / cellSize + 1);
        var humanVisionHashMap = new NativeParallelHashMap<int, int>(cellCount < zombieCount ? cellCount : zombieCount, Allocator.TempJob);
        var hashMoveEscapeTargetVisionJobHandle = new HashGridPositionsCellJob
        {
            CellSize = gameControllerComponent.humanVisionDistance * 2 + 1,
            ParallelWriter = humanVisionHashMap.AsParallelWriter()
        }.ScheduleParallel(_zombieQuery, state.Dependency);

        state.Dependency = JobHandle.CombineDependencies(
            state.Dependency,
            hashMoveEscapeTargetGridPositionsJobHandle,
            hashMoveEscapeTargetVisionJobHandle
        );

        state.Dependency = new MoveEscapeFromZombiesJob
        {
            VisionDistance = gameControllerComponent.humanVisionDistance,
            HumanVisionHashMap = humanVisionHashMap,

            ZombieHashMap = zombieHashMap,
            StaticCollidablesHashMap = staticCollidableHashMap,
            DynamicCollidablesHashMap = dynamicCollidableHashMap
        }.ScheduleParallel(state.Dependency);

        zombieHashMap.Dispose(state.Dependency);
        humanVisionHashMap.Dispose(state.Dependency);
    }
}
