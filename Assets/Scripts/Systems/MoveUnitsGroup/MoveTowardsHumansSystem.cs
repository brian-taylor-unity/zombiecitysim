using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct MoveTowardsHumansJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;

    public int HearingDistance;
    [ReadOnly] public NativeParallelHashMap<uint, int> ZombieHearingHashMap;
    public int VisionDistance;
    [ReadOnly] public NativeParallelHashMap<uint, int> ZombieVisionHashMap;

    [ReadOnly] public NativeParallelHashMap<uint, int> HumanHashMap;
    [ReadOnly] public NativeParallelMultiHashMap<uint, int3> AudibleHashMap;
    [ReadOnly] public NativeParallelHashMap<uint, int> StaticCollidablesHashMap;
    [ReadOnly] public NativeParallelHashMap<uint, int> DynamicCollidablesHashMap;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, ref DesiredNextGridPosition desiredNextGridPosition, ref RandomGenerator random, [ReadOnly] in GridPosition gridPosition)
    {
        var zombieHearingHashMapCellSize = HearingDistance * 2 + 1;
        var zombieVisionHashMapCellSize = VisionDistance * 2 + 1;

        var myGridPositionValue = gridPosition.Value;
        var nearestTarget = myGridPositionValue;
        var moved = false;
        var foundTarget = false;
        var foundBySight = ZombieVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / zombieVisionHashMapCellSize), out _) ||
                           ZombieVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / zombieVisionHashMapCellSize), out _) ||
                           ZombieVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / zombieVisionHashMapCellSize), out _) ||
                           ZombieVisionHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / zombieVisionHashMapCellSize), out _);

        if (foundBySight)
        {
            foundBySight = false;

            // Get nearest target
            // Check all grid positions that are checkDist away in the x or y direction
            for (var checkDist = 1; checkDist <= VisionDistance && !foundTarget; checkDist++)
            {
                float nearestDistance = (checkDist + 2) * (checkDist + 2);
                for (var z = -checkDist; z <= checkDist; z++)
                {
                    for (var x = -checkDist; x <= checkDist; x++)
                    {
                        if (math.abs(x) != checkDist && math.abs(z) != checkDist)
                            continue;

                        var targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);
                        var targetKey = math.hash(targetGridPosition);

                        if (checkDist > VisionDistance || !HumanHashMap.TryGetValue(targetKey, out _))
                            continue;

                        // Check if we have line of sight to the target
                        if (!LineOfSightUtilities.InLineOfSightUpdated(myGridPositionValue, targetGridPosition, StaticCollidablesHashMap))
                            continue;

                        var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                        var nearest = distance < nearestDistance;

                        nearestDistance = math.select(nearestDistance, distance, nearest);
                        nearestTarget = math.select(nearestTarget, targetGridPosition, nearest);

                        foundBySight = true;
                        foundTarget = true;
                    }
                }
            }
        }

        if (!foundBySight)
        {
            var foundByHearing = ZombieHearingHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x - HearingDistance, myGridPositionValue.y, myGridPositionValue.z - HearingDistance) / zombieHearingHashMapCellSize), out _) ||
                                 ZombieHearingHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x + HearingDistance, myGridPositionValue.y, myGridPositionValue.z - HearingDistance) / zombieHearingHashMapCellSize), out _) ||
                                 ZombieHearingHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x - HearingDistance, myGridPositionValue.y, myGridPositionValue.z + HearingDistance) / zombieHearingHashMapCellSize), out _) ||
                                 ZombieHearingHashMap.TryGetValue(math.hash(new int3(myGridPositionValue.x + HearingDistance, myGridPositionValue.y, myGridPositionValue.z + HearingDistance) / zombieHearingHashMapCellSize), out _);

            if (foundByHearing)
            {
                // Get nearest target
                // Check all grid positions that are checkDist away in the x or y direction
                for (var checkDist = 1; checkDist <= HearingDistance && !foundTarget; checkDist++)
                {
                    float nearestDistance = (checkDist + 2) * (checkDist + 2);
                    for (var z = -checkDist; z <= checkDist; z++)
                    {
                        for (var x = -checkDist; x <= checkDist; x++)
                        {
                            if (math.abs(x) != checkDist && math.abs(z) != checkDist)
                                continue;

                            var targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);
                            var targetKey = math.hash(targetGridPosition);

                            if (checkDist > HearingDistance || !AudibleHashMap.TryGetFirstValue(targetKey, out var audibleTarget, out _))
                                continue;

                            var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                            var nearest = distance < nearestDistance;

                            nearestDistance = math.select(nearestDistance, distance, nearest);
                            nearestTarget = math.select(nearestTarget, audibleTarget, nearest);

                            foundTarget = true;
                        }
                    }
                }
            }
        }

        var leftMoveAvail = true;
        var leftMoveChecked = false;
        var rightMoveAvail = true;
        var rightMoveChecked = false;
        var downMoveAvail = true;
        var downMoveChecked = false;
        var upMoveAvail = true;
        var upMoveChecked = false;

        var moveLeftKey = math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
        var moveRightKey = math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
        var moveDownKey = math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
        var moveUpKey = math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));

        if (foundTarget)
        {
            var direction = nearestTarget - myGridPositionValue;
            if (math.abs(direction.x) >= math.abs(direction.z))
            {
                // Move horizontally
                if (direction.x < 0)
                {
                    leftMoveChecked = true;
                    if (StaticCollidablesHashMap.TryGetValue(moveLeftKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveLeftKey, out _))
                    {
                        leftMoveAvail = false;
                    }
                    else
                    {
                        myGridPositionValue.x--;
                        moved = true;
                    }
                }
                else if (direction.x > 0)
                {
                    rightMoveChecked = true;
                    if (StaticCollidablesHashMap.TryGetValue(moveRightKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveRightKey, out _))
                    {
                        rightMoveAvail = false;
                    }
                    else
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
                    downMoveChecked = true;
                    if (StaticCollidablesHashMap.TryGetValue(moveDownKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveDownKey, out _))
                    {
                        downMoveAvail = false;
                    }
                    else
                    {
                        myGridPositionValue.z--;
                        moved = true;
                    }
                }
                else if (direction.z > 0)
                {
                    upMoveChecked = true;
                    if (StaticCollidablesHashMap.TryGetValue(moveUpKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveUpKey, out _))
                    {
                        downMoveAvail = false;
                    }
                    else
                    {
                        myGridPositionValue.z++;
                        moved = true;
                    }
                }

                // Unit  wanted to move vertically but couldn't, so check if it wants to move horizontally
                if (!moved)
                {
                    // Move horizontally
                    if (direction.x < 0)
                    {
                        if (!leftMoveChecked)
                        {
                            leftMoveChecked = true;
                            if (StaticCollidablesHashMap.TryGetValue(moveLeftKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveLeftKey, out _))
                                leftMoveAvail = false;
                        }

                        if (leftMoveAvail)
                        {
                            myGridPositionValue.x--;
                            moved = true;
                        }
                    }
                    else if (direction.x > 0)
                    {
                        if (!rightMoveChecked)
                        {
                            rightMoveChecked = true;
                            if (StaticCollidablesHashMap.TryGetValue(moveRightKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveRightKey, out _))
                                rightMoveAvail = false;
                        }

                        if (rightMoveAvail)
                        {
                            myGridPositionValue.x++;
                            moved = true;
                        }
                    }
                }
            }

            // If a unit is close, set 'moved = true' so we don't move randomly
            if ((math.abs(direction.x) == 1 && math.abs(direction.z) == 0) || math.abs(direction.x) == 0 && math.abs(direction.z) == 1)
                moved = true;
        }

        if (!moved)
        {
            var randomDirIndex = random.Value.NextInt(0, 4);
            for (var i = 0; i < 4 && !moved; i++)
            {
                var direction = (randomDirIndex + i) % 4;
                switch (direction)
                {
                    case 0:
                        if (!upMoveChecked)
                        {
                            upMoveChecked = true;
                            upMoveAvail = !(StaticCollidablesHashMap.TryGetValue(moveUpKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveUpKey, out _));
                        }

                        if (upMoveAvail)
                        {
                            myGridPositionValue.z += 1;
                            moved = true;
                        }
                        break;
                    case 1:
                        if (!rightMoveChecked)
                        {
                            rightMoveChecked = true;
                            rightMoveAvail = !(StaticCollidablesHashMap.TryGetValue(moveRightKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveRightKey, out _));
                        }

                        if (rightMoveAvail)
                        {
                            myGridPositionValue.x += 1;
                            moved = true;
                        }
                        break;
                    case 2:
                        if (!downMoveChecked)
                        {
                            downMoveChecked = true;
                            downMoveAvail = !(StaticCollidablesHashMap.TryGetValue(moveDownKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveDownKey, out _));
                        }

                        if (downMoveAvail)
                        {
                            myGridPositionValue.z -= 1;
                            moved = true;
                        }
                        break;
                    case 3:
                        if (!leftMoveChecked)
                        {
                            leftMoveChecked = true;
                            leftMoveAvail = !(StaticCollidablesHashMap.TryGetValue(moveLeftKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveLeftKey, out _));
                        }

                        if (leftMoveAvail)
                        {
                            myGridPositionValue.x -= 1;
                            moved = true;
                        }
                        break;
                }
            }
        }

        if (foundBySight)
        {
            var audibleEntity = Ecb.CreateEntity(entityIndexInQuery);
            Ecb.AddComponent(entityIndexInQuery, audibleEntity, new Audible { GridPositionValue = myGridPositionValue, Target = nearestTarget, Age = 0 });
        }

        desiredNextGridPosition = new DesiredNextGridPosition { Value = myGridPositionValue };
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
[RequireMatchingQueriesForUpdate]
public partial struct MoveTowardsHumansSystem : ISystem
{
    private EntityQuery _moveTowardsHumanQuery;
    private EntityQuery _humanQuery;
    private EntityQuery _audibleQuery;

    public void OnCreate(ref SystemState state)
    {
        _moveTowardsHumanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GridPosition, MoveTowardsHuman, TurnActive>()
            .WithAllRW<DesiredNextGridPosition, RandomGenerator>()
        );
        _humanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Human, GridPosition>());
        _audibleQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Audible>());

        state.RequireForUpdate<EndInitializationEntityCommandBufferSystem.Singleton>();
        state.RequireForUpdate<HashStaticCollidableSystemComponent>();
        state.RequireForUpdate<HashDynamicCollidableSystemComponent>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireForUpdate(_moveTowardsHumanQuery);
        state.RequireAnyForUpdate(_humanQuery, _audibleQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var staticCollidableComponent = SystemAPI.GetSingleton<HashStaticCollidableSystemComponent>();
        var dynamicCollidableComponent = SystemAPI.GetSingleton<HashDynamicCollidableSystemComponent>();
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        state.Dependency = JobHandle.CombineDependencies(state.Dependency, staticCollidableComponent.Handle, dynamicCollidableComponent.Handle);

        var cellSize = gameControllerComponent.zombieVisionDistance * 2 + 1;
        var cellCount = math.asint(math.ceil((float)gameControllerComponent.numTilesX / cellSize * gameControllerComponent.numTilesY / cellSize));
        var humanCount = _humanQuery.CalculateEntityCount();
        var humanHashMap = new NativeParallelHashMap<uint, int>(humanCount, Allocator.TempJob);
        var zombieVisionHashMap = new NativeParallelHashMap<uint, int>(cellCount < humanCount ? cellCount : humanCount, Allocator.TempJob);

        var hashFollowTargetGridPositionsJobHandle = state.Dependency;
        var hashFollowTargetVisionJobHandle = state.Dependency;
        if (humanCount > 0)
        {
            hashFollowTargetGridPositionsJobHandle = new HashGridPositionsJob { ParallelWriter = humanHashMap.AsParallelWriter() }.ScheduleParallel(_humanQuery, state.Dependency);
            hashFollowTargetVisionJobHandle = new HashGridPositionsCellJob
            {
                CellSize = cellSize,
                ParallelWriter = zombieVisionHashMap.AsParallelWriter()
            }.ScheduleParallel(_humanQuery, state.Dependency);
        }

        cellSize = gameControllerComponent.zombieHearingDistance * 2 + 1;
        cellCount = math.asint(math.ceil((float)gameControllerComponent.numTilesX / cellSize * gameControllerComponent.numTilesY / cellSize));
        var audibleCount = _audibleQuery.CalculateEntityCount();
        var audibleHashMap = new NativeParallelMultiHashMap<uint, int3>(audibleCount, Allocator.TempJob);
        var zombieHearingHashMap = new NativeParallelHashMap<uint, int>(cellCount < audibleCount ? cellCount : audibleCount, Allocator.TempJob);

        var hashAudiblesJobHandle = state.Dependency;
        var hashHearingJobHandle = state.Dependency;
        if (audibleCount > 0)
        {
            hashAudiblesJobHandle = new HashAudiblesJob { ParallelWriter = audibleHashMap.AsParallelWriter() }.ScheduleParallel(_audibleQuery, state.Dependency);
            hashHearingJobHandle = new HashAudiblesCellJob
            {
                CellSize = cellSize,
                ParallelWriter = zombieHearingHashMap.AsParallelWriter()
            }.ScheduleParallel(_audibleQuery, state.Dependency);
        }

        state.Dependency = JobHandle.CombineDependencies(
            state.Dependency,
            hashFollowTargetGridPositionsJobHandle,
            hashFollowTargetVisionJobHandle
        );
        state.Dependency = JobHandle.CombineDependencies(
            state.Dependency,
            hashAudiblesJobHandle,
            hashHearingJobHandle
        );

        state.Dependency = new MoveTowardsHumansJob
        {
            Ecb = SystemAPI.GetSingleton<EndInitializationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter(),

            HearingDistance = gameControllerComponent.zombieHearingDistance,
            ZombieHearingHashMap = zombieHearingHashMap,
            VisionDistance = gameControllerComponent.zombieVisionDistance,
            ZombieVisionHashMap = zombieVisionHashMap,

            HumanHashMap = humanHashMap,
            AudibleHashMap = audibleHashMap,
            StaticCollidablesHashMap = staticCollidableComponent.HashMap,
            DynamicCollidablesHashMap = dynamicCollidableComponent.HashMap,
        }.ScheduleParallel(_moveTowardsHumanQuery, state.Dependency);

        zombieHearingHashMap.Dispose(state.Dependency);
        zombieVisionHashMap.Dispose(state.Dependency);

        humanHashMap.Dispose(state.Dependency);
        audibleHashMap.Dispose(state.Dependency);
    }
}
