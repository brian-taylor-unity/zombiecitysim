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
    private ComponentGroup m_MovableGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> collidableHashMap;
        public NativeArray<GridPosition> copyCollidableGridPositions;
        public NativeArray<GridPosition> initialMovableGridPositions;
        public NativeArray<GridPosition> updatedMovableGridPositions;
        public NativeMultiHashMap<int, int> movableInitialHashMap;
        public NativeMultiHashMap<int, int> movableUpdatedHashMap;
    }

    [BurstCompile]
    struct HashGridPositionsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeMultiHashMap<int, int>.Concurrent hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(gridPositions[index].Value);
            hashMap.Add(hash, index);
        }
    }

    [BurstCompile]
    struct TryMovementJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeArray<GridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> collidableHashMap;
        [ReadOnly] public int tick;

        public void Execute(int index)
        {
            int3 myGridPositionValue = gridPositions[index].Value;

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
            if (collidableHashMap.TryGetFirstValue(rightDirKey, out _, out _))
                rightMoveAvail = false;
            if (collidableHashMap.TryGetFirstValue(downDirKey, out _, out _))
                downMoveAvail = false;
            if (collidableHashMap.TryGetFirstValue(leftDirKey, out _, out _))
                leftMoveAvail = false;

            // Pick a random direction to move
            uint seed = (uint) (tick * GridHash.Hash(myGridPositionValue));
            if (seed == 0)
                seed += (uint)tick;

            Random rand = new Random(seed);
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
                    nextGridPositions[index] = new GridPosition { Value = myGridPositionValue };
                    break;
                }
            }
        }
    }

    [BurstCompile]
    struct RevertCollidedMovementJob : IJobNativeMultiHashMapMergedSharedKeyIndices
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeArray<GridPosition> nextGridPositions;

        public void ExecuteFirst(int index)
        {
            // This was the first unit added
            // Debug.Log("[ExecuteFirst] index: " + index + " gridPosition: " + gridPositions[index].Value + " nextGridPosition: " + nextGridPositions[index].Value);
        }

        public void ExecuteNext(int innerIndex, int index)
        {
            // Debug.Log("[ExecuteNext] index: " + index + " innerIndex: " + innerIndex + " gridPosition: " + gridPositions[index].Value + " nextGridPosition: " + nextGridPositions[index].Value);
            nextGridPositions[index] = gridPositions[index];
        }
    }

    [BurstCompile]
    struct FinalizeMovementJob : IJobProcessComponentData<Position, GridPosition, Movable>
    {
        [ReadOnly] public NativeMultiHashMap<int, int> gridHashMap;
        [ReadOnly] public NativeArray<GridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> nextGridHashMap;

        public void Execute(ref Position position, ref GridPosition gridPosition, [ReadOnly] ref Movable moveable)
        {
            bool found = gridHashMap.TryGetFirstValue(GridHash.Hash(gridPosition.Value), out int index, out _);
            position = new Position { Value = new float3(nextGridPositions[index].Value) };



            gridPosition = nextGridPositions[index];

            // Debug.Log("found: " + found + " index: " + index + " gridPosition: " + gridPosition.Value);
        }
    }

    protected override void OnCreateManager()
    {
        m_CollidableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Collidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_MovableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Movable)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Position))
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.collidableHashMap.IsCreated)
            m_PrevGridState.collidableHashMap.Dispose();
        if (m_PrevGridState.copyCollidableGridPositions.IsCreated)
            m_PrevGridState.copyCollidableGridPositions.Dispose();
        if (m_PrevGridState.movableInitialHashMap.IsCreated)
            m_PrevGridState.movableInitialHashMap.Dispose();
        if (m_PrevGridState.movableUpdatedHashMap.IsCreated)
            m_PrevGridState.movableUpdatedHashMap.Dispose();
        if (m_PrevGridState.initialMovableGridPositions.IsCreated)
            m_PrevGridState.initialMovableGridPositions.Dispose();
        if (m_PrevGridState.updatedMovableGridPositions.IsCreated)
            m_PrevGridState.updatedMovableGridPositions.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var collidableGridPositions = m_CollidableGroup.GetComponentDataArray<GridPosition>();
        var collidableCount = collidableGridPositions.Length;
        var collidableHashMap = new NativeMultiHashMap<int, int>(collidableCount, Allocator.TempJob);
        var copyCollidableGridPositions = new NativeArray<GridPosition>(collidableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var movablePositions = m_MovableGroup.GetComponentDataArray<Position>();
        var movableGridPositions = m_MovableGroup.GetComponentDataArray<GridPosition>();
        var movableCount = movablePositions.Length;
        var initialMovableGridPositions = new NativeArray<GridPosition>(movableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var updatedMovableGridPositions = new NativeArray<GridPosition>(movableCount, Allocator.TempJob);
        var movableInitialHashMap = new NativeMultiHashMap<int, int>(movableCount, Allocator.TempJob);
        var movableUpdatedHashMap = new NativeMultiHashMap<int, int>(movableCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            collidableHashMap = collidableHashMap,
            copyCollidableGridPositions = copyCollidableGridPositions,
            initialMovableGridPositions = initialMovableGridPositions,
            updatedMovableGridPositions = updatedMovableGridPositions,
            movableInitialHashMap = movableInitialHashMap,
            movableUpdatedHashMap = movableUpdatedHashMap,
        };

        if (m_PrevGridState.collidableHashMap.IsCreated)
            m_PrevGridState.collidableHashMap.Dispose();
        if (m_PrevGridState.copyCollidableGridPositions.IsCreated)
            m_PrevGridState.copyCollidableGridPositions.Dispose();
        if (m_PrevGridState.movableInitialHashMap.IsCreated)
            m_PrevGridState.movableInitialHashMap.Dispose();
        if (m_PrevGridState.movableUpdatedHashMap.IsCreated)
            m_PrevGridState.movableUpdatedHashMap.Dispose();
        if (m_PrevGridState.initialMovableGridPositions.IsCreated)
            m_PrevGridState.initialMovableGridPositions.Dispose();
        if (m_PrevGridState.updatedMovableGridPositions.IsCreated)
            m_PrevGridState.updatedMovableGridPositions.Dispose();

        m_PrevGridState = nextGridState;

        var copyCollidableGridPositionsJob = new CopyComponentData<GridPosition>
        {
            Source = collidableGridPositions,
            Results = copyCollidableGridPositions,
        };
        var copyCollidableGridPositionsJobHandle = copyCollidableGridPositionsJob.Schedule(collidableCount, 2, inputDeps);

        var copyMovableGridPositionsJob = new CopyComponentData<GridPosition>
        {
            Source = movableGridPositions,
            Results = initialMovableGridPositions
        };
        var copyMovableGridPositionsJobHandle = copyMovableGridPositionsJob.Schedule(movableCount, 2, inputDeps);

        var hashCollidablePositionsJob = new HashGridPositionsJob
        {
            gridPositions = copyCollidableGridPositions,
            hashMap = collidableHashMap.ToConcurrent(),
        };
        var hashCollidablePositionsJobHandle = hashCollidablePositionsJob.Schedule(collidableCount, 64, copyCollidableGridPositionsJobHandle);

        var hashMovableInitialGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = initialMovableGridPositions,
            hashMap = movableInitialHashMap.ToConcurrent(),
        };
        var hashMovableInitialGridPositionsJobHandle = hashMovableInitialGridPositionsJob.Schedule(movableCount, 64, copyMovableGridPositionsJobHandle);

        var movementBarrierHandle = JobHandle.CombineDependencies(hashCollidablePositionsJobHandle, hashMovableInitialGridPositionsJobHandle);

        var tryMovementJob = new TryMovementJob
        {
            gridPositions = initialMovableGridPositions,
            nextGridPositions = updatedMovableGridPositions,
            collidableHashMap = collidableHashMap,
            tick = Time.frameCount,
        };
        var tryMovementJobHandle = tryMovementJob.Schedule(movableCount, 64, movementBarrierHandle);

        var hashMovableUpdatedGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = updatedMovableGridPositions,
            hashMap = movableUpdatedHashMap.ToConcurrent(),
        };
        var hashMovableUpdatedGridPositionsJobHandle = hashMovableUpdatedGridPositionsJob.Schedule(movableCount, 64, tryMovementJobHandle);

        var revertCollidedMovementJob = new RevertCollidedMovementJob
        {
            gridPositions = initialMovableGridPositions,
            nextGridPositions = updatedMovableGridPositions,
        };
        var revertCollidedMovementJobHandle = revertCollidedMovementJob.Schedule(movableUpdatedHashMap, 64, hashMovableUpdatedGridPositionsJobHandle);

        var finalizeMovementJob = new FinalizeMovementJob
        {
            gridHashMap = movableInitialHashMap,
            nextGridHashMap = movableUpdatedHashMap,
            nextGridPositions = updatedMovableGridPositions,
        };
        var finalizeMovementJobHandle = finalizeMovementJob.Schedule(this, revertCollidedMovementJobHandle);

        return finalizeMovementJobHandle;
    }
}
