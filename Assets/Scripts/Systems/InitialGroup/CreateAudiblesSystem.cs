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
        public NativeArray<GridPosition> followTargetArray;
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

    protected override void OnDestroy()
    {
        if (m_PrevGridState.followTargetArray.IsCreated)
            m_PrevGridState.followTargetArray.Dispose();
        if (m_PrevGridState.followTargetHashMap.IsCreated)
            m_PrevGridState.followTargetHashMap.Dispose();
    }

    [BurstCompile]
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeMultiHashMap<int, int>.ParallelWriter hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(gridPositions[index].Value);
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

                            int targetKey = GridHash.Hash(targetGridPosition);
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

        var followTargetArray = m_FollowTargetGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var followTargetCount = followTargetArray.Length;
        var followTargetHashMap = new NativeMultiHashMap<int, int>(followTargetCount, Allocator.TempJob);

        var audibleArchetype = EntityManager.CreateArchetype(typeof(Audible));

        var nextGridState = new PrevGridState
        {
            followTargetArray = followTargetArray,
            followTargetHashMap = followTargetHashMap,
        };
        if (m_PrevGridState.followTargetArray.IsCreated)
            m_PrevGridState.followTargetArray.Dispose();
        if (m_PrevGridState.followTargetHashMap.IsCreated)
            m_PrevGridState.followTargetHashMap.Dispose();

        m_PrevGridState = nextGridState;

        var hashTargetGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = followTargetArray,
            hashMap = followTargetHashMap.AsParallelWriter(),
        };
        var hashTargetGridPositionsJobHandle = hashTargetGridPositionsJob.Schedule(followTargetCount, 64, inputDeps);

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
