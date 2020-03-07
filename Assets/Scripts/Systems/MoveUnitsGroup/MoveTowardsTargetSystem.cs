using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
public class MoveTowardsTargetSystem : JobComponentSystem
{
    private BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;

    private EntityQuery m_FollowTargetQuery;
    private NativeHashMap<int, int> m_FollowTargetHashMap;
    private EntityQuery m_AudibleQuery;
    private NativeMultiHashMap<int, int3> m_AudibleHashMap;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override void OnStopRunning()
    {
        if (m_FollowTargetHashMap.IsCreated)
            m_FollowTargetHashMap.Dispose();

        if (m_AudibleHashMap.IsCreated)
            m_AudibleHashMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        inputDeps = JobHandle.CombineDependencies(inputDeps,
            World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableJobHandle,
            World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableJobHandle
        );

        var staticCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMap;
        var dynamicCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMap;

        if (m_FollowTargetHashMap.IsCreated)
            m_FollowTargetHashMap.Dispose();

        var followTargetCount = m_FollowTargetQuery.CalculateEntityCount();
        m_FollowTargetHashMap = new NativeHashMap<int, int>(followTargetCount, Allocator.TempJob);

        var followTargetParallelWriter = m_FollowTargetHashMap.AsParallelWriter();
        var hashFollowTargetGridPositionsJobHandle = Entities
            .WithName("HashFollowTargetGridPositions")
            .WithAll<FollowTarget>()
            .WithStoreEntityQueryInField(ref m_FollowTargetQuery)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    followTargetParallelWriter.TryAdd(hash, entityInQueryIndex);
                })
            .Schedule(inputDeps);

        if (m_AudibleHashMap.IsCreated)
            m_AudibleHashMap.Dispose();

        var audibleCount = m_AudibleQuery.CalculateEntityCount();
        m_AudibleHashMap = new NativeMultiHashMap<int, int3>(audibleCount, Allocator.TempJob);

        var audibleParallelWriter = m_AudibleHashMap.AsParallelWriter();
        var hashAudiblesJobHandle = Entities
            .WithName("HashAudibles")
            .WithStoreEntityQueryInField(ref m_AudibleQuery)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in Audible audible) =>
            {
                var hash = (int)math.hash(audible.GridPositionValue);
                audibleParallelWriter.Add(hash, audible.Target);
            })
            .Schedule(inputDeps);

        var movementBarrierHandle = JobHandle.CombineDependencies(hashFollowTargetGridPositionsJobHandle, hashAudiblesJobHandle);

        var viewDistance = GameController.instance.zombieVisionDistance;
        var hearingDistance = GameController.instance.zombieHearingDistance;
        var followTargetHashMap = m_FollowTargetHashMap;
        var audibleHashMap = m_AudibleHashMap;
        var Commands = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var audibleArchetype = Archetypes.AudibleArchetype;

        var moveTowardsTargetJobHandle = Entities
            .WithName("MoveTowardsTargets")
            .WithAll<MoveTowardsTarget>()
            .WithReadOnly(staticCollidableHashMap)
            .WithReadOnly(dynamicCollidableHashMap)
            .WithReadOnly(followTargetHashMap)
            .WithReadOnly(audibleHashMap)
            .WithBurst()
            .ForEach((Entity entity, int entityInQueryIndex, ref NextGridPosition nextGridPosition, in TurnsUntilActive turnsUntilActive, in GridPosition gridPosition) =>
                {
                    if (turnsUntilActive.Value != 0)
                        return;

                    int3 myGridPositionValue = gridPosition.Value;
                    bool moved = false;

                    // Get nearest visible target
                    // Check all grid positions that are checkDist away in the x or y direction
                    bool foundTarget = false;
                    bool foundBySight = false;
                    int3 nearestTarget = myGridPositionValue;
                    for (int checkDist = 1; checkDist <= viewDistance && !foundTarget; checkDist++)
                    {
                        float nearestDistance = (checkDist + 1) * (checkDist + 1);
                        for (int z = -checkDist; z <= checkDist; z++)
                        {
                            for (int x = -checkDist; x <= checkDist; x++)
                            {
                                if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                                {
                                    var targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);

                                    int targetKey = (int)math.hash(targetGridPosition);
                                    if (followTargetHashMap.TryGetValue(targetKey, out _))
                                    {
                                        var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                                        var nearest = distance < nearestDistance;

                                        nearestDistance = math.select(nearestDistance, distance, nearest);
                                        nearestTarget = math.select(nearestTarget, targetGridPosition, nearest);

                                        foundTarget = true;
                                        foundBySight = true;
                                    }
                                }
                            }
                        }
                    }

                    // Check for Audible entities in range
                    for (int checkDist = 1; checkDist <= hearingDistance && !foundTarget; checkDist++)
                    {
                        float nearestDistance = 300;
                        for (int z = -checkDist; z <= checkDist; z++)
                        {
                            for (int x = -checkDist; x <= checkDist; x++)
                            {
                                if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                                {
                                    var targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);

                                    int targetKey = (int)math.hash(targetGridPosition);
                                    if (audibleHashMap.TryGetFirstValue(targetKey, out int3 audibleTarget, out _))
                                    {
                                        var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(audibleTarget));
                                        var nearest = distance < nearestDistance;

                                        nearestDistance = math.select(nearestDistance, distance, nearest);
                                        nearestTarget = math.select(nearestTarget, audibleTarget, nearest);

                                        foundTarget = true;
                                    }
                                }
                            }
                        }
                    }

                    if (foundTarget)
                    {
                        int3 direction = nearestTarget - myGridPositionValue;

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

                    if (foundBySight)
                    {
                        Entity audibleEntity = Commands.CreateEntity(entityInQueryIndex, audibleArchetype);
                        Commands.SetComponent(entityInQueryIndex, audibleEntity, new Audible { GridPositionValue = myGridPositionValue, Target = nearestTarget, Age = 0 });
                    }

                    nextGridPosition = new NextGridPosition { Value = myGridPositionValue };
                })
            .Schedule(movementBarrierHandle);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(moveTowardsTargetJobHandle);

        return moveTowardsTargetJobHandle;
    }
}
