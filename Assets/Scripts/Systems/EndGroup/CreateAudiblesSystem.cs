using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

[UpdateInGroup(typeof(EndGroup))]
public class CreateAudiblesSystem : JobComponentSystem
{
    private EntityQuery query;
    private BeginInitializationEntityCommandBufferSystem m_EntityCommandBufferSystem;
    private NativeHashMap<int, int> m_FollowTargetHashMap;

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginInitializationEntityCommandBufferSystem>();
    }

    protected override void OnDestroy()
    {
        if (m_FollowTargetHashMap.IsCreated)
            m_FollowTargetHashMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var followTargetCount = query.CalculateEntityCount();

        if (m_FollowTargetHashMap.IsCreated)
            m_FollowTargetHashMap.Dispose();

        m_FollowTargetHashMap = new NativeHashMap<int, int>(followTargetCount, Allocator.TempJob);

        var hashMap = m_FollowTargetHashMap;
        var parallelWriter = m_FollowTargetHashMap.AsParallelWriter();

        var hashTargetGridPositionsJobHandle = Entities
            .WithName("HashFollowTargets")
            .WithStoreEntityQueryInField(ref query)
            .WithAll<FollowTarget>()
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var hash = (int)math.hash(gridPosition.Value);
                    parallelWriter.TryAdd(hash, entityInQueryIndex);
                })
            .Schedule(inputDeps);

        var detectDistance = GameController.instance.zombieHearingDistance;
        var Commands = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var audibleArchetype = Archetypes.AudibleArchetype;

        var createAudiblesFromZombiesJobHandle = Entities
            .WithName("CreateAudiblesFromZombies")
            .WithAll<Zombie, MoveTowardsTarget>()
            .WithReadOnly(hashMap)
            .WithBurst()
            .ForEach((int entityInQueryIndex, in GridPosition gridPosition) =>
                {
                    var myGridPositionValue = gridPosition.Value;

                    for (int checkDist = 1; checkDist < detectDistance; checkDist++)
                    {
                        for (int z = -checkDist; z < checkDist; z++)
                        {
                            for (int x = -checkDist; x < checkDist; x++)
                            {
                                if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                                {
                                    int3 targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + z);

                                    int targetKey = (int)math.hash(targetGridPosition);
                                    if (hashMap.TryGetValue(targetKey, out _))
                                    {
                                        Entity audibleEntity = Commands.CreateEntity(entityInQueryIndex, audibleArchetype);
                                        Commands.SetComponent(entityInQueryIndex, audibleEntity, new Audible { GridPositionValue = myGridPositionValue, Target = targetGridPosition, Age = 0 });
                                    }
                                }
                            }
                        }
                    }
                })
            .Schedule(hashTargetGridPositionsJobHandle);

        m_EntityCommandBufferSystem.AddJobHandleForProducer(createAudiblesFromZombiesJobHandle);

        return createAudiblesFromZombiesJobHandle;
    }
}
