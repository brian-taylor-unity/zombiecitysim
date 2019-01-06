//using Unity.Collections;
//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Burst;

//public class MovementSystem : JobComponentSystem
//{
//    private ComponentGroup m_CollidableGroup;

//    [BurstCompile]
//    struct HashCollidablePositions : IJobParallelFor
//    {
//        [ReadOnly] public ComponentDataArray<GridPosition> positions;
//        public NativeHashMap<int, int>.Concurrent hashMap;

//        public void Execute(int index)
//        {
//            var hash = GridHash.Hash(positions[index].Value);
//            hashMap.TryAdd(hash, index);
//        }
//    }

//    protected override void OnCreateManager()
//    {
//        m_CollidableGroup = GetComponentGroup(
//            ComponentType.ReadOnly(typeof(Collidable)),
//            ComponentType.ReadOnly(typeof(GridPosition))
//        );
//    }

//    protected override JobHandle OnUpdate(JobHandle inputDeps)
//    {
//        var positions = m_CollidableGroup.GetComponentDataArray<GridPosition>();
//        var collidableCount = positions.Length;
//        var hashMap = new NativeHashMap<int, int>(collidableCount, Allocator.TempJob);

//        var hashCollidablePositionsJob = new HashCollidablePositions
//        {
//            positions = positions,
//            hashMap = hashMap.ToConcurrent()
//        };

//        var hashCollidablePositionsJobHandle = hashCollidablePositionsJob.Schedule(collidableCount, 64, inputDeps);

//        hashMap.Dispose();

//        return hashCollidablePositionsJobHandle;
//    }
//}
