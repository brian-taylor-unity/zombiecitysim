using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public class MovementSystem : JobComponentSystem
{
    private ComponentGroup m_NonMovableCollidableGroup;
    private ComponentGroup m_MovableCollidableGroup;
    private ComponentGroup m_MovableGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeArray<GridPosition> nativeNonMovableCollidableGridPositions;
        public NativeMultiHashMap<int, int> nonMovableCollidableHashMap;

        public NativeArray<GridPosition> nativeMovableCollidableGridPositions;
        public NativeMultiHashMap<int, int> movableCollidableHashMap;

        public NativeArray<GridPosition> initialMovableGridPositions;
        public NativeMultiHashMap<int, int> initialMovableHashMap;

        public NativeArray<GridPosition> updatedMovableGridPositions;
        public NativeMultiHashMap<int, int> updatedMovableHashMap;
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
    struct TryRandomMovementJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeArray<GridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> nonMovableCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> movableCollidableHashMap;
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

            if (nonMovableCollidableHashMap.TryGetFirstValue(upDirKey, out _, out _) || movableCollidableHashMap.TryGetFirstValue(upDirKey, out _, out _))
                upMoveAvail = false;
            if (nonMovableCollidableHashMap.TryGetFirstValue(rightDirKey, out _, out _) || movableCollidableHashMap.TryGetFirstValue(rightDirKey, out _, out _))
                rightMoveAvail = false;
            if (nonMovableCollidableHashMap.TryGetFirstValue(downDirKey, out _, out _) || movableCollidableHashMap.TryGetFirstValue(downDirKey, out _, out _))
                downMoveAvail = false;
            if (nonMovableCollidableHashMap.TryGetFirstValue(leftDirKey, out _, out _) || movableCollidableHashMap.TryGetFirstValue(leftDirKey, out _, out _))
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
        m_NonMovableCollidableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Collidable)),
            ComponentType.ReadOnly(typeof(GridPosition))
        );
        m_MovableCollidableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Collidable)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Movable))
        );
        m_MovableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Movable)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Position))
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.nativeNonMovableCollidableGridPositions.IsCreated)
            m_PrevGridState.nativeNonMovableCollidableGridPositions.Dispose();
        if (m_PrevGridState.nonMovableCollidableHashMap.IsCreated)
            m_PrevGridState.nonMovableCollidableHashMap.Dispose();

        if (m_PrevGridState.nativeMovableCollidableGridPositions.IsCreated)
            m_PrevGridState.nativeMovableCollidableGridPositions.Dispose();
        if (m_PrevGridState.movableCollidableHashMap.IsCreated)
            m_PrevGridState.movableCollidableHashMap.Dispose();

        if (m_PrevGridState.initialMovableGridPositions.IsCreated)
            m_PrevGridState.initialMovableGridPositions.Dispose();
        if (m_PrevGridState.initialMovableHashMap.IsCreated)
            m_PrevGridState.initialMovableHashMap.Dispose();

        if (m_PrevGridState.updatedMovableGridPositions.IsCreated)
            m_PrevGridState.updatedMovableGridPositions.Dispose();
        if (m_PrevGridState.updatedMovableHashMap.IsCreated)
            m_PrevGridState.updatedMovableHashMap.Dispose();
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        int nonMovableCollidableCount = 0;
        NativeArray<GridPosition> nativeNonMovableCollidableGridPositions;
        NativeMultiHashMap<int, int> nonMovableCollidableHashMap;

        var movableCollidableGridPositions = m_MovableCollidableGroup.GetComponentDataArray<GridPosition>();
        var movableCollidableCount = movableCollidableGridPositions.Length;
        var nativeMovableCollidableGridPositions = new NativeArray<GridPosition>(movableCollidableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var movableCollidableHashMap = new NativeMultiHashMap<int, int>(movableCollidableCount, Allocator.TempJob);

        var movableGridPositions = m_MovableGroup.GetComponentDataArray<GridPosition>();
        var movableCount = movableGridPositions.Length;
        var initialMovableGridPositions = new NativeArray<GridPosition>(movableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var initialMovableHashMap = new NativeMultiHashMap<int, int>(movableCount, Allocator.TempJob);
        var updatedMovableGridPositions = new NativeArray<GridPosition>(movableCount, Allocator.TempJob);
        var updatedMovableHashMap = new NativeMultiHashMap<int, int>(movableCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            nativeNonMovableCollidableGridPositions = m_PrevGridState.nativeNonMovableCollidableGridPositions,
            nonMovableCollidableHashMap = m_PrevGridState.nonMovableCollidableHashMap,

            nativeMovableCollidableGridPositions = nativeMovableCollidableGridPositions,
            movableCollidableHashMap = movableCollidableHashMap,

            initialMovableGridPositions = initialMovableGridPositions,
            initialMovableHashMap = initialMovableHashMap,

            updatedMovableGridPositions = updatedMovableGridPositions,
            updatedMovableHashMap = updatedMovableHashMap,
        };

        JobHandle hashNonMovableCollidablePositionsJobHandle = inputDeps;
        if (m_PrevGridState.nativeNonMovableCollidableGridPositions.IsCreated)
        {
            nativeNonMovableCollidableGridPositions = m_PrevGridState.nativeNonMovableCollidableGridPositions;
            nonMovableCollidableCount = nativeNonMovableCollidableGridPositions.Length;
            nonMovableCollidableHashMap = m_PrevGridState.nonMovableCollidableHashMap;
        }
        else
        {
            ComponentDataArray<GridPosition> nonMovableCollidableGridPositions = m_NonMovableCollidableGroup.GetComponentDataArray<GridPosition>();
            nonMovableCollidableCount = nonMovableCollidableGridPositions.Length;
            nextGridState.nativeNonMovableCollidableGridPositions = nativeNonMovableCollidableGridPositions = new NativeArray<GridPosition>(nonMovableCollidableCount, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
            nextGridState.nonMovableCollidableHashMap = nonMovableCollidableHashMap = new NativeMultiHashMap<int, int>(nonMovableCollidableCount, Allocator.Persistent);

            var copyNativeNonMovableCollidableGridPositionsJob = new CopyComponentData<GridPosition>
            {
                Source = nonMovableCollidableGridPositions,
                Results = nativeNonMovableCollidableGridPositions,
            };
            var copyNativeNonMovableCollidableGridPositionsJobHandle = copyNativeNonMovableCollidableGridPositionsJob.Schedule(nonMovableCollidableCount, 2, inputDeps);

            var hashNonMovableCollidablePositionsJob = new HashGridPositionsJob
            {
                gridPositions = nativeNonMovableCollidableGridPositions,
                hashMap = nonMovableCollidableHashMap.ToConcurrent(),
            };
            hashNonMovableCollidablePositionsJobHandle = hashNonMovableCollidablePositionsJob.Schedule(nonMovableCollidableCount, 64, copyNativeNonMovableCollidableGridPositionsJobHandle);
        }

        if (m_PrevGridState.nativeMovableCollidableGridPositions.IsCreated)
            m_PrevGridState.nativeMovableCollidableGridPositions.Dispose();
        if (m_PrevGridState.movableCollidableHashMap.IsCreated)
            m_PrevGridState.movableCollidableHashMap.Dispose();

        if (m_PrevGridState.initialMovableGridPositions.IsCreated)
            m_PrevGridState.initialMovableGridPositions.Dispose();
        if (m_PrevGridState.initialMovableHashMap.IsCreated)
            m_PrevGridState.initialMovableHashMap.Dispose();

        if (m_PrevGridState.updatedMovableGridPositions.IsCreated)
            m_PrevGridState.updatedMovableGridPositions.Dispose();
        if (m_PrevGridState.updatedMovableHashMap.IsCreated)
            m_PrevGridState.updatedMovableHashMap.Dispose();

        m_PrevGridState = nextGridState;

        var copyNativeMovableCollidableGridPositionsJob = new CopyComponentData<GridPosition>
        {
            Source = movableCollidableGridPositions,
            Results = nativeMovableCollidableGridPositions,
        };
        var copyNativeMovableCollidableGridPositionsJobHandle = copyNativeMovableCollidableGridPositionsJob.Schedule(movableCollidableCount, 2, inputDeps);

        var hashMovableCollidablePositionsJob = new HashGridPositionsJob
        {
            gridPositions = nativeMovableCollidableGridPositions,
            hashMap = movableCollidableHashMap.ToConcurrent(),
        };
        var hashMovableCollidablePositionsJobHandle = hashMovableCollidablePositionsJob.Schedule(movableCollidableCount, 64, copyNativeMovableCollidableGridPositionsJobHandle);

        var copyNativeMovableGridPositionsJob = new CopyComponentData<GridPosition>
        {
            Source = movableGridPositions,
            Results = initialMovableGridPositions
        };
        var copyNativeMovableGridPositionsJobHandle = copyNativeMovableGridPositionsJob.Schedule(movableCount, 2, inputDeps);

        var hashMovableInitialGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = initialMovableGridPositions,
            hashMap = initialMovableHashMap.ToConcurrent(),
        };
        var hashMovableInitialGridPositionsJobHandle = hashMovableInitialGridPositionsJob.Schedule(movableCount, 64, copyNativeMovableGridPositionsJobHandle);

        var movementBarrierHandle = JobHandle.CombineDependencies(hashNonMovableCollidablePositionsJobHandle, hashMovableCollidablePositionsJobHandle, hashMovableInitialGridPositionsJobHandle);

        var tryRandomMovementJob = new TryRandomMovementJob
        {
            gridPositions = initialMovableGridPositions,
            nextGridPositions = updatedMovableGridPositions,
            nonMovableCollidableHashMap = nonMovableCollidableHashMap,
            movableCollidableHashMap = movableCollidableHashMap,
            tick = Time.frameCount,
        };
        var tryRandomMovementJobHandle = tryRandomMovementJob.Schedule(movableCount, 64, movementBarrierHandle);

        var hashMovableUpdatedGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = updatedMovableGridPositions,
            hashMap = updatedMovableHashMap.ToConcurrent(),
        };
        var hashMovableUpdatedGridPositionsJobHandle = hashMovableUpdatedGridPositionsJob.Schedule(movableCount, 64, tryRandomMovementJobHandle);

        var revertCollidedMovementJob = new RevertCollidedMovementJob
        {
            gridPositions = initialMovableGridPositions,
            nextGridPositions = updatedMovableGridPositions,
        };
        var revertCollidedMovementJobHandle = revertCollidedMovementJob.Schedule(updatedMovableHashMap, 64, hashMovableUpdatedGridPositionsJobHandle);

        var finalizeMovementJob = new FinalizeMovementJob
        {
            gridHashMap = initialMovableHashMap,
            nextGridHashMap = updatedMovableHashMap,
            nextGridPositions = updatedMovableGridPositions,
        };
        var finalizeMovementJobHandle = finalizeMovementJob.Schedule(this, revertCollidedMovementJobHandle);

        return finalizeMovementJobHandle;
    }
}
