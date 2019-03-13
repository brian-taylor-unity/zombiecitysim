using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Transforms;

public class CreateAudiblesBarrier : BarrierSystem
{
}

[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public class CreateAudiblesSystem : JobComponentSystem
{
    private ComponentGroup m_FollowTargetGroup;
    private PrevGridState m_PrevGridState;

    [Inject] private CreateAudiblesBarrier m_CreateAudiblesBarrier;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> followTargetHashMap;
    }

    [BurstCompile]
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> gridPositions;
        public NativeMultiHashMap<int, int>.Concurrent hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(gridPositions[index].Value);
            hashMap.Add(hash, index);
        }
    }

    struct CreateAudiblesFromTargetsJob : IJobProcessComponentDataWithEntity<MoveTowardsTarget, GridPosition>
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
                                Commands.SetComponent(index, audibleEntity, new Position { Value = new float3(myGridPositionValue) });
                                Commands.SetComponent(index, audibleEntity, new GridPosition { Value = myGridPositionValue });
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
        var followTargetComponentArray = m_FollowTargetGroup.GetComponentDataArray<GridPosition>();
        var followTargetCount = followTargetComponentArray.Length;
        var followTargetHashMap = new NativeMultiHashMap<int, int>(followTargetCount, Allocator.TempJob);

        var Commands = m_CreateAudiblesBarrier.CreateCommandBuffer().ToConcurrent();

        var nextGridState = new PrevGridState
        {
            followTargetHashMap = followTargetHashMap,
        };
        if (m_PrevGridState.followTargetHashMap.IsCreated)
            m_PrevGridState.followTargetHashMap.Dispose();
        
        m_PrevGridState = nextGridState;

        var hashTargetGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = followTargetComponentArray,
            hashMap = followTargetHashMap.ToConcurrent(),
        };
        var hashTargetGridPositionsJobHandle = hashTargetGridPositionsJob.Schedule(followTargetCount, 64, inputDeps);

        var createAudiblesFromTargetsJob = new CreateAudiblesFromTargetsJob
        {
            targetHashMap = followTargetHashMap,
            detectDistance = Bootstrap.ZombieVisionDistance,
            Commands = Commands,
            archetype = Bootstrap.AudibleArchetype,
        };
        var createAudiblesFromTargetsJobHandle = createAudiblesFromTargetsJob.Schedule(this, hashTargetGridPositionsJobHandle);

        return createAudiblesFromTargetsJobHandle;
    }

    protected override void OnCreateManager()
    {
        m_FollowTargetGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(FollowTarget)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.followTargetHashMap.IsCreated)
            m_PrevGridState.followTargetHashMap.Dispose();
    }
}
