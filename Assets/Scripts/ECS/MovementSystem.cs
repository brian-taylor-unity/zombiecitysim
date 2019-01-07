using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;

public class MovementSystem : JobComponentSystem
{
    private ComponentGroup m_CollidableGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeHashMap<int, int> hashMap;
    }

    [BurstCompile]
    struct HashCollidablePositions : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> positions;
        public NativeHashMap<int, int>.Concurrent hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(positions[index].Value);
            hashMap.TryAdd(hash, index);
        }
    }

    protected override void OnCreateManager()
    {
        m_CollidableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Collidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
    }

    protected override void OnDestroyManager()
    {
        m_PrevGridState.hashMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var positions = m_CollidableGroup.GetComponentDataArray<GridPosition>();
        var collidableCount = positions.Length;
        var hashMap = new NativeHashMap<int, int>(collidableCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            hashMap = hashMap,
        };
        if (m_PrevGridState.hashMap.IsCreated)
            m_PrevGridState.hashMap.Dispose();
        m_PrevGridState = nextGridState;

        var hashCollidablePositionsJob = new HashCollidablePositions
        {
            positions = positions,
            hashMap = hashMap.ToConcurrent()
        };
        var hashCollidablePositionsJobHandle = hashCollidablePositionsJob.Schedule(collidableCount, 64, inputDeps);



        return hashCollidablePositionsJobHandle;
    }
}
