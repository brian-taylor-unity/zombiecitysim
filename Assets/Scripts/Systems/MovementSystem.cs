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
    private ComponentGroup m_HumanMovableGroup;
    private ComponentGroup m_ZombieMovableGroup;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> nonMovableCollidableHashMap;
        public NativeMultiHashMap<int, int> movableCollidableHashMap;
        public NativeMultiHashMap<int, int> initialHumanGridPositionsHashMap;
        public NativeMultiHashMap<int, int> updatedHumanGridPositionsHashMap;
        public NativeMultiHashMap<int, int> initialZombieGridPositionsHashMap;
        public NativeMultiHashMap<int, int> updatedZombieGridPositionsHashMap;
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
    struct HashGridPositionsNativeArrayJob : IJobParallelFor
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
    struct TryFollowMovementJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> gridPositions;
        public NativeArray<GridPosition> nextGridPositions;
        public ComponentDataArray<PrevMoveDirection> prevMoveDirectionArray;
        [ReadOnly] public NativeMultiHashMap<int, int> nonMovableCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> movableCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> targetGridPositionsHashMap;
        public int viewDistance;

        public void Execute(int index)
        {
            int3 myGridPositionValue = gridPositions[index].Value;
            bool moved = false;

            // Get nearest target
            // Check all grid positions that are checkDist away in the x or y direction
            bool foundTarget = false;
            int3 nearestTarget = myGridPositionValue;
            float nearestDistance = viewDistance * viewDistance;
            for (int y = -viewDistance; y < viewDistance; y++)
            {
                for (int x = -viewDistance; x < viewDistance; x++)
                {
                    if (x != 0 || y != 0)
                    {
                        int3 targetGridPosition = new int3(myGridPositionValue.x + x, myGridPositionValue.y, myGridPositionValue.z + y);
                        int targetKey = GridHash.Hash(targetGridPosition);
                        if (targetGridPositionsHashMap.TryGetFirstValue(targetKey, out _, out _))
                        {
                            var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(targetGridPosition));
                            var nearest = distance < nearestDistance;

                            nearestDistance = math.@select(nearestDistance, distance, nearest);
                            nearestTarget = targetGridPosition;

                            foundTarget = true;
                        }
                    }
                }
            }

            if (foundTarget)
            {
                int3 direction = nearestTarget - myGridPositionValue;

                // Check if space is already occupied
                int moveLeftKey = GridHash.Hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
                int moveRightKey = GridHash.Hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
                int moveDownKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
                int moveUpKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
                if (math.abs(direction.x) >= math.abs(direction.z))
                {
                    // Move horizontally
                    if (direction.x < 0)
                    {
                        if (!nonMovableCollidableHashMap.TryGetFirstValue(moveLeftKey, out _, out _) &&
                            !movableCollidableHashMap.TryGetFirstValue(moveLeftKey, out _, out _))
                        {
                            myGridPositionValue.x--;
                            moved = true;
                        }
                    }
                    else if (direction.x > 0)
                    {
                        if (!nonMovableCollidableHashMap.TryGetFirstValue(moveRightKey, out _, out _) &&
                            !movableCollidableHashMap.TryGetFirstValue(moveRightKey, out _, out _))
                        {
                            myGridPositionValue.x++;
                            moved = true;
                        }
                    }
                }
                // Unit maybe wanted to move horizontally but couldn't, so check if it wants to move vertically
                if (!moved)
                {
                    // Move vertically
                    if (direction.z < 0)
                    {
                        if (!nonMovableCollidableHashMap.TryGetFirstValue(moveDownKey, out _, out _) &&
                            !movableCollidableHashMap.TryGetFirstValue(moveDownKey, out _, out _))
                        {
                            myGridPositionValue.z--;
                        }
                    }
                    else if (direction.z > 0)
                    {
                        if (!nonMovableCollidableHashMap.TryGetFirstValue(moveUpKey, out _, out _) &&
                            !movableCollidableHashMap.TryGetFirstValue(moveUpKey, out _, out _))
                        {
                            myGridPositionValue.z++;
                        }
                    }
                }
            }

            //if (!moved && !prevMoveDirectionArray[index].Value.Equals(new int3(0, 0, 0)))
            //{
            //    // Try to move in the same direction as last turn
            //    int movePrevKey = GridHash.Hash(myGridPositionValue + prevMoveDirectionArray[index].Value);
            //    if (!nonMovableCollidableHashMap.TryGetFirstValue(movePrevKey, out _, out _) &&
            //        !movableCollidableHashMap.TryGetFirstValue(movePrevKey, out _, out _))
            //    {
            //        myGridPositionValue += prevMoveDirectionArray[index].Value;
            //    }
            //}

            prevMoveDirectionArray[index] = new PrevMoveDirection { Value = myGridPositionValue - gridPositions[index].Value };
            nextGridPositions[index] = new GridPosition { Value = myGridPositionValue };
        }
    }

    [BurstCompile]
    struct TryRandomMovementJob : IJobParallelFor
    {
        [ReadOnly] public ComponentDataArray<GridPosition> gridPositions;
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
            uint seed = (uint)(tick * GridHash.Hash(myGridPositionValue));
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
                    break;
            }
            nextGridPositions[index] = new GridPosition { Value = myGridPositionValue };
        }
    }

    [BurstCompile]
    struct ResolveCollidedMovementJob : IJobNativeMultiHashMapMergedSharedKeyIndices
    {
        [ReadOnly] public NativeArray<GridPosition> updatedGridPositions;
        public ComponentDataArray<GridPosition> gridPositionComponentData;
        public ComponentDataArray<Position> positionComponentData;
        [ReadOnly] public NativeMultiHashMap<int, int> updatedCollisionGridPositionsHashMap;

        public void ExecuteFirst(int index)
        {
            // This was the first unit added
            int seed = GridHash.Hash(updatedGridPositions[index].Value);
            if (updatedCollisionGridPositionsHashMap.TryGetFirstValue(seed, out _, out _))
                return;

            gridPositionComponentData[index] = updatedGridPositions[index];
            positionComponentData[index] = new Position { Value = new float3(updatedGridPositions[index].Value) };
        }

        public void ExecuteNext(int innerIndex, int index)
        {
            // Don't move this unit
        }
    }

    [BurstCompile]
    struct DeallocateJob : IJob
    {
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<GridPosition> updatedHumanGridPositions;
        [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<GridPosition> updatedZombieGridPositions;

        public void Execute()
        {
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        NativeMultiHashMap<int, int> nonMovableCollidableHashMap;

        var movableCollidableGridPositions = m_MovableCollidableGroup.GetComponentDataArray<GridPosition>();
        var movableCollidableCount = movableCollidableGridPositions.Length;
        var movableCollidableHashMap = new NativeMultiHashMap<int, int>(movableCollidableCount, Allocator.TempJob);

        var humanMovableGridPositions = m_HumanMovableGroup.GetComponentDataArray<GridPosition>();
        var humanMovablePositions = m_HumanMovableGroup.GetComponentDataArray<Position>();
        var humanMovableCount = humanMovableGridPositions.Length;
        var initialHumanGridPositionsHashMap = new NativeMultiHashMap<int, int>(humanMovableCount, Allocator.TempJob);
        var updatedHumanGridPositions = new NativeArray<GridPosition>(humanMovableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var updatedHumanGridPositionsHashMap = new NativeMultiHashMap<int, int>(humanMovableCount, Allocator.TempJob);

        var zombieMovableGridPositions = m_ZombieMovableGroup.GetComponentDataArray<GridPosition>();
        var zombiePrevMoveDirectionArray = m_ZombieMovableGroup.GetComponentDataArray<PrevMoveDirection>();
        var zombieMovablePositions = m_ZombieMovableGroup.GetComponentDataArray<Position>();
        var zombieMovableCount = zombieMovableGridPositions.Length;
        var initialZombieGridPositionsHashMap = new NativeMultiHashMap<int, int>(zombieMovableCount, Allocator.TempJob);
        var updatedZombieGridPositions = new NativeArray<GridPosition>(zombieMovableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var updatedZombieGridPositionsHashMap = new NativeMultiHashMap<int, int>(zombieMovableCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            nonMovableCollidableHashMap = m_PrevGridState.nonMovableCollidableHashMap,
            movableCollidableHashMap = movableCollidableHashMap,
            initialHumanGridPositionsHashMap = initialHumanGridPositionsHashMap,
            updatedHumanGridPositionsHashMap = updatedHumanGridPositionsHashMap,
            initialZombieGridPositionsHashMap = initialZombieGridPositionsHashMap,
            updatedZombieGridPositionsHashMap = updatedZombieGridPositionsHashMap,
        };

        JobHandle hashNonMovableCollidablePositionsJobHandle = inputDeps;
        if (m_PrevGridState.nonMovableCollidableHashMap.IsCreated)
        {
            nonMovableCollidableHashMap = m_PrevGridState.nonMovableCollidableHashMap;
        }
        else
        {
            ComponentDataArray<GridPosition> nonMovableCollidableGridPositions = m_NonMovableCollidableGroup.GetComponentDataArray<GridPosition>();
            var nonMovableCollidableCount = nonMovableCollidableGridPositions.Length;
            nextGridState.nonMovableCollidableHashMap = nonMovableCollidableHashMap = new NativeMultiHashMap<int, int>(nonMovableCollidableCount, Allocator.Persistent);

            var hashNonMovableCollidablePositionsJob = new HashGridPositionsJob
            {
                gridPositions = nonMovableCollidableGridPositions,
                hashMap = nonMovableCollidableHashMap.ToConcurrent(),
            };
            hashNonMovableCollidablePositionsJobHandle = hashNonMovableCollidablePositionsJob.Schedule(nonMovableCollidableCount, 64, inputDeps);
        }

        if (m_PrevGridState.movableCollidableHashMap.IsCreated)
            m_PrevGridState.movableCollidableHashMap.Dispose();
        if (m_PrevGridState.initialHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialHumanGridPositionsHashMap.Dispose();
        if (m_PrevGridState.updatedHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedHumanGridPositionsHashMap.Dispose();
        if (m_PrevGridState.initialZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialZombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.updatedZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedZombieGridPositionsHashMap.Dispose();

        m_PrevGridState = nextGridState;

        var hashMovableCollidablePositionsJob = new HashGridPositionsJob
        {
            gridPositions = movableCollidableGridPositions,
            hashMap = movableCollidableHashMap.ToConcurrent(),
        };
        var hashMovableCollidablePositionsJobHandle = hashMovableCollidablePositionsJob.Schedule(movableCollidableCount, 64, inputDeps);

        var hashInitialHumanGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = humanMovableGridPositions,
            hashMap = initialHumanGridPositionsHashMap.ToConcurrent(),
        };
        var hashInitialHumanGridPositionsJobHandle = hashInitialHumanGridPositionsJob.Schedule(humanMovableCount, 64, inputDeps);

        var hashInitialZombieGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = zombieMovableGridPositions,
            hashMap = initialZombieGridPositionsHashMap.ToConcurrent(),
        };
        var hashInitialZombieGridPositionsJobHandle = hashInitialZombieGridPositionsJob.Schedule(zombieMovableCount, 64, inputDeps);

        var humanMovementBarrierHandle = JobHandle.CombineDependencies(hashNonMovableCollidablePositionsJobHandle, hashMovableCollidablePositionsJobHandle, hashInitialHumanGridPositionsJobHandle);

        var tryRandomMovementJob = new TryRandomMovementJob
        {
            gridPositions = humanMovableGridPositions,
            nextGridPositions = updatedHumanGridPositions,
            nonMovableCollidableHashMap = nonMovableCollidableHashMap,
            movableCollidableHashMap = movableCollidableHashMap,
            tick = Time.frameCount,
        };
        var tryRandomMovementJobHandle = tryRandomMovementJob.Schedule(humanMovableCount, 64, humanMovementBarrierHandle);

        var zombieMovementBarrier = JobHandle.CombineDependencies(hashNonMovableCollidablePositionsJobHandle, hashMovableCollidablePositionsJobHandle, hashInitialZombieGridPositionsJobHandle);
        zombieMovementBarrier = JobHandle.CombineDependencies(zombieMovementBarrier, hashInitialHumanGridPositionsJobHandle);

        var tryFollowMovementJob = new TryFollowMovementJob
        {
            gridPositions = zombieMovableGridPositions,
            nextGridPositions = updatedZombieGridPositions,
            prevMoveDirectionArray = zombiePrevMoveDirectionArray,
            nonMovableCollidableHashMap = nonMovableCollidableHashMap,
            movableCollidableHashMap = movableCollidableHashMap,
            targetGridPositionsHashMap = initialHumanGridPositionsHashMap,
            viewDistance = Bootstrap.ZombieVisionDistance,
        };
        var tryFollowMovementJobHandle = tryFollowMovementJob.Schedule(zombieMovableCount, 64, zombieMovementBarrier);

        var finalizeMovementBarrier = JobHandle.CombineDependencies(tryRandomMovementJobHandle, tryFollowMovementJobHandle);

        var hashHumanUpdatedGridPositionsJob = new HashGridPositionsNativeArrayJob
        {
            gridPositions = updatedHumanGridPositions,
            hashMap = updatedHumanGridPositionsHashMap.ToConcurrent(),
        };
        var hashHumanUpdatedGridPositionsJobHandle = hashHumanUpdatedGridPositionsJob.Schedule(humanMovableCount, 64, finalizeMovementBarrier);

        var hashZombieUpdatedGridPositionsJob = new HashGridPositionsNativeArrayJob
        {
            gridPositions = updatedZombieGridPositions,
            hashMap = updatedZombieGridPositionsHashMap.ToConcurrent(),
        };
        var hashZombieUpdatedGridPositionsJobHandle = hashZombieUpdatedGridPositionsJob.Schedule(zombieMovableCount, 64, finalizeMovementBarrier);

        var resolveMovementBarrier = JobHandle.CombineDependencies(hashHumanUpdatedGridPositionsJobHandle, hashZombieUpdatedGridPositionsJobHandle);

        var resolveCollidedZombieMovementJob = new ResolveCollidedMovementJob
        {
            updatedGridPositions = updatedZombieGridPositions,
            gridPositionComponentData = zombieMovableGridPositions,
            positionComponentData = zombieMovablePositions,
            updatedCollisionGridPositionsHashMap = updatedHumanGridPositionsHashMap,
        };
        var resolveCollidedZombieMovementJobHandle = resolveCollidedZombieMovementJob.Schedule(updatedZombieGridPositionsHashMap, 64, resolveMovementBarrier);

        var resolveCollidedHumanMovementJob = new ResolveCollidedMovementJob
        {
            updatedGridPositions = updatedHumanGridPositions,
            gridPositionComponentData = humanMovableGridPositions,
            positionComponentData = humanMovablePositions,
            updatedCollisionGridPositionsHashMap = updatedZombieGridPositionsHashMap,
        };
        var resolveCollidedHumanMovementJobHandle = resolveCollidedHumanMovementJob.Schedule(updatedHumanGridPositionsHashMap, 64, resolveCollidedZombieMovementJobHandle);

        var deallocateJob = new DeallocateJob
        {
            updatedHumanGridPositions = updatedHumanGridPositions,
            updatedZombieGridPositions = updatedZombieGridPositions,
        };
        var deallocateJobHandle = deallocateJob.Schedule(resolveCollidedHumanMovementJobHandle);

        return deallocateJobHandle;
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
        m_HumanMovableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(Movable)),
            typeof(GridPosition),
            typeof(Position)
        );
        m_ZombieMovableGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Zombie)),
            ComponentType.ReadOnly(typeof(Movable)),
            typeof(GridPosition),
            typeof(PrevMoveDirection),
            typeof(Position)
        );
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.nonMovableCollidableHashMap.IsCreated)
            m_PrevGridState.nonMovableCollidableHashMap.Dispose();
        if (m_PrevGridState.movableCollidableHashMap.IsCreated)
            m_PrevGridState.movableCollidableHashMap.Dispose();
        if (m_PrevGridState.initialHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialHumanGridPositionsHashMap.Dispose();
        if (m_PrevGridState.updatedHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedHumanGridPositionsHashMap.Dispose();
        if (m_PrevGridState.initialZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialZombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.updatedZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedZombieGridPositionsHashMap.Dispose();
    }
}
