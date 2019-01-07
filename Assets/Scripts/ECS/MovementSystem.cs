using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Entities;
using Unity.Jobs;
using Unity.Burst;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class MovementSystem : JobComponentSystem
{
    private ComponentGroup m_CollidableGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> collidableHashMap;
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

    [BurstCompile]
    struct TryMovementJob : IJobProcessComponentData<GridPosition, Movable>
    {
        [ReadOnly] public NativeMultiHashMap<int, int> collidableHashMap;
        [ReadOnly] public int tick;

        public void Execute(ref GridPosition gridPosition, [ReadOnly] ref Movable moveable)
        {
            int3 myGridPositionValue = gridPosition.Value;

            int upDirKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
            int rightDirKey = GridHash.Hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
            int downDirKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
            int leftDirKey = GridHash.Hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));

            bool upMoveAvail = true;
            bool rightMoveAvail = true;
            bool downMoveAvail = true;
            bool leftMoveAvail = true;

            if (collidableHashMap.TryGetFirstValue(upDirKey, out _, out _))
                upMoveAvail = false;
            if (collidableHashMap.TryGetFirstValue(upDirKey, out _, out _))
                rightMoveAvail = false;
            if (collidableHashMap.TryGetFirstValue(upDirKey, out _, out _))
                downMoveAvail = false;
            if (collidableHashMap.TryGetFirstValue(upDirKey, out _, out _))
                leftMoveAvail = false;

            // Pick a random direction to move
            Random rand = new Random((uint)(tick ^ GridHash.Hash(myGridPositionValue)));
            int randomDirIndex = rand.NextInt(0, 4);

            bool moved = false;
            for (int i = 0; i < 4; i++)
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

                if (moved)
                {
                    gridPosition.Value = myGridPositionValue;
                    break;
                }
            }
        }
    }

    [BurstCompile]
    struct FinalizeMovementJob : IJobNativeMultiHashMapMergedSharedKeyIndices
    {

        public NativeArray<Position> positions;

        public void ExecuteFirst(int index)
        {
            // This was the first unit added, update its Position

        }

        public void ExecuteNext(int cellIndex, int index)
        {

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
        if (m_PrevGridState.collidableHashMap.IsCreated)
            m_PrevGridState.collidableHashMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var collidableGridPositions = m_CollidableGroup.GetComponentDataArray<GridPosition>();
        var collidableCount = collidableGridPositions.Length;
        var collidableHashMap = new NativeMultiHashMap<int, int>(collidableCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            collidableHashMap = collidableHashMap,
        };
        if (m_PrevGridState.collidableHashMap.IsCreated)
            m_PrevGridState.collidableHashMap.Dispose();

        m_PrevGridState = nextGridState;

        var hashCollidablePositionsJob = new HashGridPositionsJob
        {
            gridPositions = collidableGridPositions,
            hashMap = collidableHashMap.ToConcurrent(),
        };
        var hashCollidablePositionsJobHandle = hashCollidablePositionsJob.Schedule(collidableCount, 64, inputDeps);

        var tryMovementJob = new TryMovementJob
        {
            collidableHashMap = collidableHashMap,
            tick = Time.frameCount,
        };
        var tryMovementJobHandle = tryMovementJob.Schedule(this, hashCollidablePositionsJobHandle);

        if (collidableHashMap.IsCreated)
            collidableHashMap.Dispose();
        collidableHashMap = new NativeMultiHashMap<int, int>(collidableCount, Allocator.Temp);
        hashCollidablePositionsJob = new HashGridPositionsJob
        {
            gridPositions = collidableGridPositions,
            hashMap = collidableHashMap.ToConcurrent(),
        };
        hashCollidablePositionsJobHandle =
            hashCollidablePositionsJob.Schedule(collidableCount, 64, tryMovementJobHandle);

        var finalizeMovementJob = new FinalizeMovementJob
        {

        };
        var finalizeMovementJobHandle =
            finalizeMovementJob.Schedule(collidableHashMap, 64, hashCollidablePositionsJobHandle);

        return tryMovementJobHandle;
    }
}
