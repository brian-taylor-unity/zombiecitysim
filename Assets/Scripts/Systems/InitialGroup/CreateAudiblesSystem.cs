using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;

[UpdateInGroup(typeof(InitialGroup))]
public class CreateAudiblesSystem : JobComponentSystem
{
    EntityCommandBufferSystem m_EntityCommandBufferSystem;

    private EntityQuery m_MoveTowardsTargetGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> moveTowardsTargetArray;
        public NativeMultiHashMap<int, int> moveTowardsTargetHashMap;
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

    struct CreateAudiblesFromTargetsJob : IJobForEachWithEntity<FollowTarget, GridPosition>
    {
        [ReadOnly] public NativeMultiHashMap<int, int> audibleGeneratorHashMap;
        public int detectDistance;
        public EntityCommandBuffer.Concurrent Commands;
        public EntityArchetype archetype;

        public void Execute(Entity entity, int index, [ReadOnly] ref FollowTarget followTarget, [ReadOnly] ref GridPosition gridPosition)
        {
            var targetGridPositionValue = gridPosition.Value;

            for (int checkDist = 1; checkDist < detectDistance; checkDist++)
            {
                for (int z = -checkDist; z < checkDist; z++)
                {
                    for (int x = -checkDist; x < checkDist; x++)
                    {
                        if (math.abs(x) == checkDist || math.abs(z) == checkDist)
                        {
                            int3 sourceGridPositionValue = new int3(targetGridPositionValue.x + x, targetGridPositionValue.y, targetGridPositionValue.z + z);

                            int sourceKey = GridHash.Hash(sourceGridPositionValue);
                            if (audibleGeneratorHashMap.TryGetFirstValue(sourceKey, out _, out _))
                            {
                                Entity audibleEntity = Commands.CreateEntity(index, archetype);
                                Commands.SetComponent(index, audibleEntity, new Audible { GridPositionValue = sourceGridPositionValue, Target = targetGridPositionValue, Age = 0 });
                                return;
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

        var moveTowardsTargetArray = m_MoveTowardsTargetGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var moveTowardsTargetCount = moveTowardsTargetArray.Length;
        var moveTowardsTargetHashMap = new NativeMultiHashMap<int, int>(moveTowardsTargetCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            moveTowardsTargetArray = moveTowardsTargetArray,
            moveTowardsTargetHashMap = moveTowardsTargetHashMap,
        };
        if (m_PrevGridState.moveTowardsTargetArray.IsCreated)
            m_PrevGridState.moveTowardsTargetArray.Dispose();
        if (m_PrevGridState.moveTowardsTargetHashMap.IsCreated)
            m_PrevGridState.moveTowardsTargetHashMap.Dispose();
        
        m_PrevGridState = nextGridState;

        var hashTargetGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = moveTowardsTargetArray,
            hashMap = moveTowardsTargetHashMap.AsParallelWriter(),
        };
        var hashTargetGridPositionsJobHandle = hashTargetGridPositionsJob.Schedule(moveTowardsTargetCount, 64, inputDeps);

        var createAudiblesFromTargetsJob = new CreateAudiblesFromTargetsJob
        {
            audibleGeneratorHashMap = moveTowardsTargetHashMap,
            detectDistance = GameController.instance.zombieVisionDistance,
            Commands = Commands,
            archetype = Bootstrap.AudibleArchetype,
        };
        var createAudiblesFromTargetsJobHandle = createAudiblesFromTargetsJob.ScheduleSingle(this, hashTargetGridPositionsJobHandle);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(createAudiblesFromTargetsJobHandle);

        return createAudiblesFromTargetsJobHandle;
    }

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EntityCommandBufferSystem>();

        m_MoveTowardsTargetGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(MoveTowardsTarget)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnStopRunning()
    {
        if (m_PrevGridState.moveTowardsTargetArray.IsCreated)
            m_PrevGridState.moveTowardsTargetArray.Dispose();
        if (m_PrevGridState.moveTowardsTargetHashMap.IsCreated)
            m_PrevGridState.moveTowardsTargetHashMap.Dispose();
    }
}
