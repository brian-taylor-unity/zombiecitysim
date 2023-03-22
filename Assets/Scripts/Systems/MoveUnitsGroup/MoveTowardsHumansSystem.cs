using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine.Serialization;

[BurstCompile]
public partial struct MoveTowardsTargetJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;

    public int HearingDistance;
    [ReadOnly] public NativeParallelHashMap<int, int> ZombieHearingHashMap;
    public int VisionDistance;
    [ReadOnly] public NativeParallelHashMap<int, int> ZombieVisionHashMap;

    [FormerlySerializedAs("FollowTargetHashMap")] [ReadOnly] public NativeParallelHashMap<int, int> HumanHashMap;
    [ReadOnly] public NativeParallelMultiHashMap<int, int3> AudibleHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> StaticCollidablesHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> DynamicCollidablesHashMap;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, ref NextGridPosition nextGridPosition, ref RandomGenerator random, in GridPosition gridPosition)
    {
        var zombieHearingHashMapCellSize = HearingDistance * 2 + 1;
        var zombieVisionHashMapCellSize = VisionDistance * 2 + 1;

        int3 myGridPositionValue = gridPosition.Value;
        int3 nearestTarget = myGridPositionValue;
        bool moved = false;
        bool foundByHearing = ZombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - HearingDistance, myGridPositionValue.y, myGridPositionValue.z - HearingDistance) / zombieHearingHashMapCellSize), out _) ||
                              ZombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + HearingDistance, myGridPositionValue.y, myGridPositionValue.z - HearingDistance) / zombieHearingHashMapCellSize), out _) ||
                              ZombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - HearingDistance, myGridPositionValue.y, myGridPositionValue.z + HearingDistance) / zombieHearingHashMapCellSize), out _) ||
                              ZombieHearingHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + HearingDistance, myGridPositionValue.y, myGridPositionValue.z + HearingDistance) / zombieHearingHashMapCellSize), out _);
        bool foundBySight = ZombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / zombieVisionHashMapCellSize), out _) ||
                            ZombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z - VisionDistance) / zombieVisionHashMapCellSize), out _) ||
                            ZombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x - VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / zombieVisionHashMapCellSize), out _) ||
                            ZombieVisionHashMap.TryGetValue((int)math.hash(new int3(myGridPositionValue.x + VisionDistance, myGridPositionValue.y, myGridPositionValue.z + VisionDistance) / zombieVisionHashMapCellSize), out _);
        bool foundTarget = foundByHearing || foundBySight;

        if (foundTarget)
        {
            foundByHearing = false;
            foundBySight = false;
            foundTarget = false;

            // Get nearest target
            // Check all grid positions that are checkDist away in the x or y direction
            for (int checkDist = 1; (checkDist <= VisionDistance || checkDist <= HearingDistance) && !foundTarget; checkDist++)
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

                            if (checkDist <= VisionDistance && HumanHashMap.TryGetValue(targetKey, out _))
                            {
                                // Check if we have line of sight to the target
                                if (LineOfSightUtilities.InLineOfSight(myGridPositionValue, targetGridPosition, StaticCollidablesHashMap))
                                {
                                    var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                                    var nearest = distance < nearestDistance;

                                    nearestDistance = math.select(nearestDistance, distance, nearest);
                                    nearestTarget = math.select(nearestTarget, targetGridPosition, nearest);

                                    foundBySight = true;
                                }
                            }

                            if (!foundBySight && checkDist <= HearingDistance && AudibleHashMap.TryGetFirstValue(targetKey, out int3 audibleTarget, out _))
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

        if (StaticCollidablesHashMap.TryGetValue(moveLeftKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveLeftKey, out _))
            leftMoveAvail = false;
        if (StaticCollidablesHashMap.TryGetValue(moveRightKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveRightKey, out _))
            rightMoveAvail = false;
        if (StaticCollidablesHashMap.TryGetValue(moveDownKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveDownKey, out _))
            downMoveAvail = false;
        if (StaticCollidablesHashMap.TryGetValue(moveUpKey, out _) || DynamicCollidablesHashMap.TryGetValue(moveUpKey, out _))
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
            Entity audibleEntity = Ecb.CreateEntity(entityIndexInQuery);
            Ecb.AddComponent(entityIndexInQuery, audibleEntity, new Audible { GridPositionValue = myGridPositionValue, Target = nearestTarget, Age = 0 });
        }

        nextGridPosition = new NextGridPosition { Value = myGridPositionValue };
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
public partial struct MoveTowardsHumansSystem : ISystem
{
    private EntityQuery _moveTowardsHumanQuery;
    private EntityQuery _humanQuery;
    private EntityQuery _audibleQuery;

    public void OnCreate(ref SystemState state)
    {
        _moveTowardsHumanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp)
            .WithAll<GridPosition, MoveTowardsHuman, TurnActive>()
            .WithAllRW<NextGridPosition, RandomGenerator>()
        );
        _humanQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Human, GridPosition>());
        _audibleQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<Audible>());

        state.RequireForUpdate<BeginInitializationEntityCommandBufferSystem.Singleton>();
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

        var humanCount = _humanQuery.CalculateEntityCount();
        var humanHashMap = new NativeParallelHashMap<int, int>(humanCount, Allocator.TempJob);
        var zombieVisionHashMap = new NativeParallelHashMap<int, int>(humanCount, Allocator.TempJob);

        var hashFollowTargetGridPositionsJobHandle = new HashGridPositionsJob
        {
            ParallelWriter = humanHashMap.AsParallelWriter()
        }.ScheduleParallel(_humanQuery, state.Dependency);

        var hashFollowTargetVisionJobHandle = new HashGridPositionsCellJob
        {
            CellSize = gameControllerComponent.zombieVisionDistance * 2 + 1,
            ParallelWriter = zombieVisionHashMap.AsParallelWriter()
        }.ScheduleParallel(_humanQuery, state.Dependency);

        var audibleCount = _audibleQuery.CalculateEntityCount();
        var audibleHashMap = new NativeParallelMultiHashMap<int, int3>(audibleCount, Allocator.TempJob);
        var zombieHearingHashMap = new NativeParallelHashMap<int, int>(audibleCount, Allocator.TempJob);

        var hashAudiblesJobHandle = new HashAudiblesJob { ParallelWriter = audibleHashMap.AsParallelWriter() }.ScheduleParallel(_audibleQuery, state.Dependency);
        var hashHearingJobHandle = new HashAudiblesCellJob
        {
            CellSize = gameControllerComponent.zombieHearingDistance * 2 + 1,
            ParallelWriter = zombieHearingHashMap.AsParallelWriter()
        }.ScheduleParallel(_audibleQuery, state.Dependency);

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
            Ecb = ecb,

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
