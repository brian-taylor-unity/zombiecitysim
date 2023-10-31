using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct MoveEscapeFromZombiesJob : IJobEntity
{
    public int VisionDistance;
    [ReadOnly] public NativeParallelHashMap<uint, int> HumanVisionHashMap;

    [ReadOnly] public NativeParallelHashMap<uint, int> ZombieHashMap;
    [ReadOnly] public NativeParallelHashMap<uint, int> StaticCollidablesHashMap;
    [ReadOnly] public NativeParallelHashMap<uint, int> DynamicCollidablesHashMap;

    public void Execute(ref DesiredNextGridPosition desiredNextGridPosition, [ReadOnly] in GridPosition gridPosition, [ReadOnly] in TurnActive turnActive)
    {
        var humanVisionHashMapCellSize = VisionDistance * 2 + 1;

        var myGridPositionValue = gridPosition.Value;
        var averageTarget = new float3(0, 0, 0);
        var targetCount = 0;
        var moved = false;

        var foundTarget = HumanVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / humanVisionHashMapCellSize), out _) ||
                          HumanVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / humanVisionHashMapCellSize), out _) ||
                          HumanVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / humanVisionHashMapCellSize), out _) ||
                          HumanVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / humanVisionHashMapCellSize), out _);

        if (foundTarget)
        {
            foundTarget = false;

            for (var checkDist = 1; checkDist <= VisionDistance && !foundTarget; checkDist++)
            {
                for (var z = -checkDist; z <= checkDist; z++)
                {
                    for (var x = -checkDist; x <= checkDist; x++)
                    {
                        if (math.abs(x) != checkDist && math.abs(z) != checkDist)
                            continue;

                        var targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);
                        var targetKey = math.hash(targetGridPosition);

                        if (!ZombieHashMap.TryGetValue(targetKey, out _))
                            continue;

                        // Check if we have line of sight to the target
                        if (!LineOfSightUtilities.InLineOfSightUpdated(myGridPositionValue, targetGridPosition, StaticCollidablesHashMap))
                            continue;

                        averageTarget += new float3(x, 0, z);
                        targetCount++;

                        foundTarget = true;
                    }
                }
            }
        }

        if (foundTarget)
        {
            averageTarget /= targetCount;
            var direction = new int3((int)-averageTarget.x, (int)averageTarget.y, (int)-averageTarget.z);

            // Check if space is already occupied
            var moveLeftKey = math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
            var moveRightKey = math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
            var moveDownKey = math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
            var moveUpKey = math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
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

        desiredNextGridPosition = new DesiredNextGridPosition { Value = myGridPositionValue };
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
        var zombieHashMap = new NativeParallelHashMap<uint, int>(zombieCount, Allocator.TempJob);
        var hashMoveEscapeTargetGridPositionsJobHandle = new HashGridPositionsJob { ParallelWriter = zombieHashMap.AsParallelWriter() }.ScheduleParallel(_zombieQuery, state.Dependency);

        var cellSize = gameControllerComponent.humanVisionDistance * 2 + 1;
        var cellCount = math.asint(math.ceil((float)gameControllerComponent.numTilesX / cellSize * gameControllerComponent.numTilesY / cellSize));
        var humanVisionHashMap = new NativeParallelHashMap<uint, int>(cellCount < zombieCount ? cellCount: zombieCount, Allocator.TempJob);
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
