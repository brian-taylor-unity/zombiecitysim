using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitialGroup))]
public class CreateAudiblesSystem : JobComponentSystem
{
    EntityCommandBufferSystem m_EntityCommandBufferSystem;

    private EntityQuery m_FollowTargetGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> followTargetHashMap;
    }

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();

        m_FollowTargetGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(FollowTarget)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnStopRunning()
    {
        if (m_PrevGridState.followTargetHashMap.IsCreated)
            m_PrevGridState.followTargetHashMap.Dispose();
    }

    [BurstCompile]
    struct HashGridPositionsJob : IJobForEachWithEntity<GridPosition>
    {
        public NativeMultiHashMap<int, int>.ParallelWriter hashMap;

        public void Execute(Entity entity, int index, [ReadOnly] ref GridPosition gridPosition)
        {
            var hash = (int)math.hash(gridPosition.Value);
            hashMap.Add(hash, index);
        }
    }

    [BurstCompile]
    struct CreateAudiblesFromTargetsJob : IJobForEachWithEntity<MoveTowardsTarget, GridPosition>
    {
        [ReadOnly] public NativeMultiHashMap<int, int> targetHashMap;
        public int detectDistance;
        public EntityCommandBuffer.Concurrent Commands;
        public EntityArchetype archetype;

        public void Execute(Entity entity, int index, [ReadOnly] ref MoveTowardsTarget moveTowardsTarget, [ReadOnly] ref GridPosition gridPosition)
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
                            if (targetHashMap.TryGetFirstValue(targetKey, out _, out _))
                            {
                                Entity audibleEntity = Commands.CreateEntity(index, archetype);
                                Commands.SetComponent(index, audibleEntity, new Audible { GridPositionValue = myGridPositionValue, Target = targetGridPosition, Age = 0 });
                            }
                        }
                    }
                }
            }

        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var Commands = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();
        var followTargetHashMap = new NativeMultiHashMap<int, int>(m_FollowTargetGroup.CalculateEntityCount(), Allocator.TempJob);
        var audibleArchetype = Archetypes.AudibleArchetype;

        var nextGridState = new PrevGridState
        {
            followTargetHashMap = followTargetHashMap,
        };
        if (m_PrevGridState.followTargetHashMap.IsCreated)
            m_PrevGridState.followTargetHashMap.Dispose();

        m_PrevGridState = nextGridState;

        var hashTargetGridPositionsJob = new HashGridPositionsJob
        {
            hashMap = followTargetHashMap.AsParallelWriter(),
        };
        var hashTargetGridPositionsJobHandle = hashTargetGridPositionsJob.Schedule(m_FollowTargetGroup, inputDeps);

        var createAudiblesFromTargetsJob = new CreateAudiblesFromTargetsJob
        {
            targetHashMap = followTargetHashMap,
            detectDistance = GameController.instance.zombieVisionDistance,
            Commands = Commands,
            archetype = audibleArchetype,
        };
        var createAudiblesFromTargetsJobHandle = createAudiblesFromTargetsJob.Schedule(this, hashTargetGridPositionsJobHandle);

        return createAudiblesFromTargetsJobHandle;
    }
}
