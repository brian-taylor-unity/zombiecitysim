using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct MoveTowardsTargetJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter ecb;

    public int hearingDistance;
    [ReadOnly] public NativeParallelHashMap<int, int> zombieHearingHashMap;
    public int visionDistance;
    [ReadOnly] public NativeParallelHashMap<int, int> zombieVisionHashMap;

    [ReadOnly] public NativeParallelHashMap<int, int> followTargetHashMap;
    [ReadOnly] public NativeParallelMultiHashMap<int, int3> audibleHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> staticCollidablesHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> dynamicCollidablesHashMap;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, ref NextGridPosition nextGridPosition, ref RandomGenerator random, in GridPosition gridPosition, in TurnsUntilActive turnsUntilActive)
    {
        if (turnsUntilActive.Value != 1)
            return;

        var zombieHearingHashMapCellSize = hearingDistance * 2 + 1;
        var zombieVisionHashMapCellSize = visionDistance * 2 + 1;

        int3 myGridPositionValue = gridPosition.Value;
        int3 nearestTarget = myGridPositionValue;
        bool moved = false;
        bool foundByHearing = zombieHearingHashMap.TryGetValue((int)math.hash(myGridPositionValue / zombieHearingHashMapCellSize), out _) ||
                              zombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - hearingDistance, myGridPositionValue.y, myGridPositionValue.z - hearingDistance) / zombieHearingHashMapCellSize), out _) ||
                              zombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + hearingDistance, myGridPositionValue.y, myGridPositionValue.z - hearingDistance) / zombieHearingHashMapCellSize), out _) ||
                              zombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - hearingDistance, myGridPositionValue.y, myGridPositionValue.z + hearingDistance) / zombieHearingHashMapCellSize), out _) ||
                              zombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + hearingDistance, myGridPositionValue.y, myGridPositionValue.z + hearingDistance) / zombieHearingHashMapCellSize), out _);
        bool foundBySight = zombieVisionHashMap.TryGetValue((int)math.hash(myGridPositionValue / zombieVisionHashMapCellSize), out _) ||
                            zombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - visionDistance, myGridPositionValue.y, myGridPositionValue.z - visionDistance) / zombieVisionHashMapCellSize), out _) ||
                            zombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + visionDistance, myGridPositionValue.y, myGridPositionValue.z - visionDistance) / zombieVisionHashMapCellSize), out _) ||
                            zombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - visionDistance, myGridPositionValue.y, myGridPositionValue.z + visionDistance) / zombieVisionHashMapCellSize), out _) ||
                            zombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + visionDistance, myGridPositionValue.y, myGridPositionValue.z + visionDistance) / zombieVisionHashMapCellSize), out _);
        bool foundTarget = foundByHearing || foundBySight;

        if (foundTarget)
        {
            foundByHearing = false;
            foundBySight = false;
            foundTarget = false;

            // Get nearest target
            // Check all grid positions that are checkDist away in the x or y direction
            for (int checkDist = 1; (checkDist <= visionDistance || checkDist <= hearingDistance) && !foundTarget; checkDist++)
            {
                float nearestDistance = (checkDist + 2) * (checkDist + 2);
                for (int z = -checkDist; z <= checkDist; z++)
                {
                    for (int x = -checkDist; x <= checkDist; x++)
                    {
                        if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                        {
                            var targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);
                            int targetKey = (int)math.hash(targetGridPosition);

                            if (checkDist <= visionDistance && followTargetHashMap.TryGetValue(targetKey, out _))
                            {
                                // Check if we have line of sight to the target
                                if (LineOfSightUtilities.InLineOfSight(myGridPositionValue, targetGridPosition, staticCollidablesHashMap))
                                {
                                    var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                                    var nearest = distance < nearestDistance;

                                    nearestDistance = math.select(nearestDistance, distance, nearest);
                                    nearestTarget = math.select(nearestTarget, targetGridPosition, nearest);

                                    foundBySight = true;
                                }
                            }

                            if (!foundBySight && checkDist <= hearingDistance && audibleHashMap.TryGetFirstValue(targetKey, out int3 audibleTarget, out _))
                            {
                                var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                                var nearest = distance < nearestDistance;

                                nearestDistance = math.select(nearestDistance, distance, nearest);
                                nearestTarget = math.select(nearestTarget, audibleTarget, nearest);

                                foundByHearing = true;
                            }
                        }

                        foundTarget = foundByHearing || foundBySight;
                    }
                }
            }
        }

        var leftMoveAvail = true;
        var rightMoveAvail = true;
        var downMoveAvail = true;
        var upMoveAvail = true;

        int moveLeftKey = (int)math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
        int moveRightKey = (int)math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
        int moveDownKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
        int moveUpKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));

        if (staticCollidablesHashMap.TryGetValue(moveLeftKey, out _) || dynamicCollidablesHashMap.TryGetValue(moveLeftKey, out _))
            leftMoveAvail = false;
        if (staticCollidablesHashMap.TryGetValue(moveRightKey, out _) || dynamicCollidablesHashMap.TryGetValue(moveRightKey, out _))
            rightMoveAvail = false;
        if (staticCollidablesHashMap.TryGetValue(moveDownKey, out _) || dynamicCollidablesHashMap.TryGetValue(moveDownKey, out _))
            downMoveAvail = false;
        if (staticCollidablesHashMap.TryGetValue(moveUpKey, out _) || dynamicCollidablesHashMap.TryGetValue(moveUpKey, out _))
            upMoveAvail = false;

        if (foundTarget)
        {
            int3 direction = nearestTarget - myGridPositionValue;
            if (math.abs(direction.x) >= math.abs(direction.z))
            {
                // Move horizontally
                if (direction.x < 0)
                {
                    if (leftMoveAvail)
                    {
                        myGridPositionValue.x--;
                        moved = true;
                    }
                }
                else if (direction.x > 0)
                {
                    if (rightMoveAvail)
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
                    if (downMoveAvail)
                    {
                        myGridPositionValue.z--;
                        moved = true;
                    }
                }
                else if (direction.z > 0)
                {
                    if (upMoveAvail)
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
                        if (leftMoveAvail)
                        {
                            myGridPositionValue.x--;
                            moved = true;
                        }
                    }
                    else if (direction.x > 0)
                    {
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
            int randomDirIndex = random.Value.NextInt(0, 4);
            for (int i = 0; i < 4 && !moved; i++)
            {
                int direction = (randomDirIndex + i) % 4;
                switch (direction)
                {
                    case 0:
                        if (upMoveAvail)
                        {
                            myGridPositionValue.z += 1;
                            moved = true;
                        }
                        break;
                    case 1:
                        if (rightMoveAvail)
                        {
                            myGridPositionValue.x += 1;
                            moved = true;
                        }
                        break;
                    case 2:
                        if (downMoveAvail)
                        {
                            myGridPositionValue.z -= 1;
                            moved = true;
                        }
                        break;
                    case 3:
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
            Entity audibleEntity = ecb.CreateEntity(entityIndexInQuery);
            ecb.AddComponent(entityIndexInQuery, audibleEntity, new Audible { GridPositionValue = myGridPositionValue, Target = nearestTarget, Age = 0 });
        }

        nextGridPosition = new NextGridPosition { Value = myGridPositionValue };
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
public partial struct MoveTowardsTargetSystem : ISystem
{
    private EntityQuery _moveTowardsTargetQuery;
    private EntityQuery _followTargetQuery;
    private EntityQuery _audibleQuery;

    public void OnCreate(ref SystemState state)
    {
        _moveTowardsTargetQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<MoveTowardsTarget>()
            .Build(ref state);
        _followTargetQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<FollowTarget>()
            .Build(ref state);
        _audibleQuery = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Audible>()
            .Build(ref state);

        state.RequireForUpdate<StaticCollidableComponent>();
        state.RequireForUpdate<DynamicCollidableComponent>();
        state.RequireForUpdate<GameControllerComponent>();
        state.RequireAnyForUpdate(_followTargetQuery, _audibleQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var staticCollidableComponent = SystemAPI.GetSingleton<StaticCollidableComponent>();
        var dynamicCollidableComponent = SystemAPI.GetSingleton<DynamicCollidableComponent>();
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        state.Dependency = JobHandle.CombineDependencies(state.Dependency, staticCollidableComponent.Handle, dynamicCollidableComponent.Handle);

        var followTargetCount = _followTargetQuery.CalculateEntityCount();
        var followTargetHashMap = new NativeParallelHashMap<int, int>(followTargetCount, Allocator.TempJob);
        var followTargetParallelWriter = followTargetHashMap.AsParallelWriter();
        // We need either "(X * Y) / visionDistance" or "numUnitsToFollow" hash buckets, whichever is smaller
        var zombieVisionHashMap = new NativeParallelHashMap<int, int>(followTargetCount, Allocator.TempJob);
        var zombieVisionParallelWriter = zombieVisionHashMap.AsParallelWriter();

        var hashFollowTargetGridPositionsJobHandle = new HashGridPositionsJob { parallelWriter = followTargetParallelWriter }.ScheduleParallel(_followTargetQuery, state.Dependency);
        var hashFollowTargetVisionJobHandle = new HashGridPositionsCellJob { cellSize = gameControllerComponent.zombieVisionDistance * 2 + 1, parallelWriter = zombieVisionParallelWriter }.ScheduleParallel(_followTargetQuery, state.Dependency);

        var audibleCount = _audibleQuery.CalculateEntityCount();
        var audibleHashMap = new NativeParallelMultiHashMap<int, int3>(audibleCount, Allocator.TempJob);
        var audibleParallelWriter = audibleHashMap.AsParallelWriter();
        // We need either "(X * Y) / visionDistance" or "audibleCount" hash buckets, whichever is smaller
        var zombieHearingHashMap = new NativeParallelHashMap<int, int>(audibleCount, Allocator.TempJob);
        var zombieHearingParallelWriter = zombieHearingHashMap.AsParallelWriter();

        var hashAudiblesJobHandle = new HashAudiblesJob { parallelWriter = audibleParallelWriter }.ScheduleParallel(_audibleQuery, state.Dependency);
        var hashHearingJobHandle = new HashAudiblesCellJob { cellSize = gameControllerComponent.zombieHearingDistance * 2 + 1, parallelWriter = zombieHearingParallelWriter }.ScheduleParallel(_audibleQuery, state.Dependency);

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

        var ecb = SystemAPI.GetSingleton<BeginInitializationEntityCommandBufferSystem.Singleton>()
            .CreateCommandBuffer(state.WorldUnmanaged)
            .AsParallelWriter();

        state.Dependency = new MoveTowardsTargetJob
        {
            ecb = ecb,

            hearingDistance = gameControllerComponent.zombieHearingDistance,
            zombieHearingHashMap = zombieHearingHashMap,
            visionDistance = gameControllerComponent.zombieVisionDistance,
            zombieVisionHashMap = zombieVisionHashMap,

            followTargetHashMap = followTargetHashMap,
            audibleHashMap = audibleHashMap,
            staticCollidablesHashMap = staticCollidableComponent.HashMap,
            dynamicCollidablesHashMap = dynamicCollidableComponent.HashMap,
        }.ScheduleParallel(_moveTowardsTargetQuery, state.Dependency);

        zombieHearingHashMap.Dispose(state.Dependency);
        zombieVisionHashMap.Dispose(state.Dependency);

        followTargetHashMap.Dispose(state.Dependency);
        audibleHashMap.Dispose(state.Dependency);
    }
}
