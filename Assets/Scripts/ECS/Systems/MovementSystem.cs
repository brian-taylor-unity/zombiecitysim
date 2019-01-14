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
    private int m_ZombieViewDistance;
    private PrevGridState m_PrevGridState;

    struct PrevGridState
    {
        public NativeMultiHashMap<int, int> nonMovableCollidableHashMap;

        public NativeArray<GridPosition> nativeMovableCollidableGridPositions;
        public NativeMultiHashMap<int, int> movableCollidableHashMap;

        public NativeArray<GridPosition> initialHumanGridPositions;
        public NativeMultiHashMap<int, int> initialHumanGridPositionsHashMap;
        public NativeArray<GridPosition> updatedHumanGridPositions;
        public NativeMultiHashMap<int, int> updatedHumanGridPositionsHashMap;

        public NativeArray<GridPosition> initialZombieGridPositions;
        public NativeMultiHashMap<int, int> initialZombieGridPositionsHashMap;
        public NativeArray<int> zombieTargetIndexArray;
        public NativeArray<int3> zombieTargetValuesArray;
        public NativeArray<GridPosition> updatedZombieGridPositions;
        public NativeMultiHashMap<int, int> updatedZombieGridPositionsHashMap;
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
    struct TryFollowMovementJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<GridPosition> gridPositions;
        public NativeArray<GridPosition> nextGridPositions;
        [ReadOnly] public NativeMultiHashMap<int, int> nonMovableCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> movableCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> targetGridPositionsHashMap;
        public int viewDistance;
        [NativeDisableParallelForRestriction] public NativeArray<int> validTargetIndexArray;
        [NativeDisableParallelForRestriction] public NativeArray<int3> validTargetsArray;

        public void Execute(int index)
        {
            int arraySliceSize = (viewDistance * 2 + 1) * (viewDistance * 2 + 1);
            var myValidTargetIndexArray = validTargetIndexArray.Slice(index * arraySliceSize, arraySliceSize);
            var myValidTargetList = validTargetsArray.Slice(index * arraySliceSize, arraySliceSize);
            int3 myGridPositionValue = gridPositions[index].Value;

            // Get nearest target
            int validIndex = 0;
            // Check all grid positions that are checkDist away in the x or y direction
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
                            int myIndex = (y + viewDistance) * viewDistance + (x + viewDistance);
                            myValidTargetIndexArray[validIndex] = myIndex;
                            myValidTargetList[myIndex] = targetGridPosition;
                        }
                    }

                    validIndex++;
                }
            }

            if (myValidTargetList.Length > 0)
            {
                // Get the closest target from our list of targets
                int nearestIndex = -1;
                float nearestDistance = math.lengthsq(new float3(myGridPositionValue) - new float3(myValidTargetList[0]));
                for (int i = 0; i < myValidTargetList.Length; i++)
                {
                    if (myValidTargetIndexArray[i] != -1)
                    {
                        var distance = math.lengthsq(new float3(myGridPositionValue) - new float3(myValidTargetList[i]));
                        var nearest = distance < nearestDistance;

                        nearestDistance = math.@select(nearestDistance, distance, nearest);
                        nearestIndex = math.@select(nearestIndex, i, nearest);
                    }
                }

                if (nearestIndex != -1)
                {
                    int3 direction = myValidTargetList[nearestIndex] - myGridPositionValue;

                    // Check if space is already occupied
                    int moveLeftKey = GridHash.Hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));
                    int moveRightKey = GridHash.Hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
                    int moveDownKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
                    int moveUpKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
                    bool moved = false;
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
            }

            nextGridPositions[index] = new GridPosition { Value = myGridPositionValue };
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

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        NativeMultiHashMap<int, int> nonMovableCollidableHashMap;

        var movableCollidableGridPositions = m_MovableCollidableGroup.GetComponentDataArray<GridPosition>();
        var movableCollidableCount = movableCollidableGridPositions.Length;
        var nativeMovableCollidableGridPositions = new NativeArray<GridPosition>(movableCollidableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var movableCollidableHashMap = new NativeMultiHashMap<int, int>(movableCollidableCount, Allocator.TempJob);

        var humanMovableGridPositions = m_HumanMovableGroup.GetComponentDataArray<GridPosition>();
        var humanMovablePositions = m_HumanMovableGroup.GetComponentDataArray<Position>();
        var humanMovableCount = humanMovableGridPositions.Length;
        var initialHumanGridPositions = new NativeArray<GridPosition>(humanMovableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var initialHumanGridPositionsHashMap = new NativeMultiHashMap<int, int>(humanMovableCount, Allocator.TempJob);
        var updatedHumanGridPositions = new NativeArray<GridPosition>(humanMovableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var updatedHumanGridPositionsHashMap = new NativeMultiHashMap<int, int>(humanMovableCount, Allocator.TempJob);

        var zombieMovableGridPositions = m_ZombieMovableGroup.GetComponentDataArray<GridPosition>();
        var zombieMovablePositions = m_ZombieMovableGroup.GetComponentDataArray<Position>();
        var zombieMovableCount = zombieMovableGridPositions.Length;
        var initialZombieGridPositions = new NativeArray<GridPosition>(zombieMovableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var initialZombieGridPositionsHashMap = new NativeMultiHashMap<int, int>(zombieMovableCount, Allocator.TempJob);
        var zombieTargetIndexArray = new NativeArray<int>(zombieMovableCount * (m_ZombieViewDistance * 2 + 1) * (m_ZombieViewDistance * 2 + 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        for (int i = 0; i < zombieTargetIndexArray.Length; i++)
        {
            zombieTargetIndexArray[i] = -1;
        }

        var zombieTargetValuesArray = new NativeArray<int3>(zombieMovableCount * (m_ZombieViewDistance * 2 + 1) * (m_ZombieViewDistance * 2 + 1), Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var updatedZombieGridPositions = new NativeArray<GridPosition>(zombieMovableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
        var updatedZombieGridPositionsHashMap = new NativeMultiHashMap<int, int>(zombieMovableCount, Allocator.TempJob);

        var nextGridState = new PrevGridState
        {
            nonMovableCollidableHashMap = m_PrevGridState.nonMovableCollidableHashMap,

            nativeMovableCollidableGridPositions = nativeMovableCollidableGridPositions,
            movableCollidableHashMap = movableCollidableHashMap,

            initialHumanGridPositions = initialHumanGridPositions,
            initialHumanGridPositionsHashMap = initialHumanGridPositionsHashMap,
            updatedHumanGridPositions = updatedHumanGridPositions,
            updatedHumanGridPositionsHashMap = updatedHumanGridPositionsHashMap,

            initialZombieGridPositions = initialZombieGridPositions,
            initialZombieGridPositionsHashMap = initialZombieGridPositionsHashMap,
            zombieTargetIndexArray = zombieTargetIndexArray,
            zombieTargetValuesArray = zombieTargetValuesArray,
            updatedZombieGridPositions = updatedZombieGridPositions,
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
            var nativeNonMovableCollidableGridPositions = new NativeArray<GridPosition>(nonMovableCollidableCount, Allocator.TempJob, NativeArrayOptions.UninitializedMemory);
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

            hashNonMovableCollidablePositionsJobHandle.Complete();
            nativeNonMovableCollidableGridPositions.Dispose();
        }

        if (m_PrevGridState.nativeMovableCollidableGridPositions.IsCreated)
            m_PrevGridState.nativeMovableCollidableGridPositions.Dispose();
        if (m_PrevGridState.movableCollidableHashMap.IsCreated)
            m_PrevGridState.movableCollidableHashMap.Dispose();

        if (m_PrevGridState.initialHumanGridPositions.IsCreated)
            m_PrevGridState.initialHumanGridPositions.Dispose();
        if (m_PrevGridState.initialHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialHumanGridPositionsHashMap.Dispose();
        if (m_PrevGridState.updatedHumanGridPositions.IsCreated)
            m_PrevGridState.updatedHumanGridPositions.Dispose();
        if (m_PrevGridState.updatedHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedHumanGridPositionsHashMap.Dispose();

        if (m_PrevGridState.initialZombieGridPositions.IsCreated)
            m_PrevGridState.initialZombieGridPositions.Dispose();
        if (m_PrevGridState.initialZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialZombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.zombieTargetIndexArray.IsCreated)
            m_PrevGridState.zombieTargetIndexArray.Dispose();
        if (m_PrevGridState.zombieTargetValuesArray.IsCreated)
            m_PrevGridState.zombieTargetValuesArray.Dispose();
        if (m_PrevGridState.updatedZombieGridPositions.IsCreated)
            m_PrevGridState.updatedZombieGridPositions.Dispose();
        if (m_PrevGridState.updatedZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedZombieGridPositionsHashMap.Dispose();

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

        var copyNativeHumanMovableGridPositionsJob = new CopyComponentData<GridPosition>
        {
            Source = humanMovableGridPositions,
            Results = initialHumanGridPositions
        };
        var copyNativeHumanGridPositionsJobHandle = copyNativeHumanMovableGridPositionsJob.Schedule(humanMovableCount, 2, inputDeps);

        var hashInitialHumanGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = initialHumanGridPositions,
            hashMap = initialHumanGridPositionsHashMap.ToConcurrent(),
        };
        var hashInitialHumanGridPositionsJobHandle = hashInitialHumanGridPositionsJob.Schedule(humanMovableCount, 64, copyNativeHumanGridPositionsJobHandle);

        var humanMovementBarrierHandle = JobHandle.CombineDependencies(hashNonMovableCollidablePositionsJobHandle, hashMovableCollidablePositionsJobHandle, hashInitialHumanGridPositionsJobHandle);

        var tryRandomMovementJob = new TryRandomMovementJob
        {
            gridPositions = initialHumanGridPositions,
            nextGridPositions = updatedHumanGridPositions,
            nonMovableCollidableHashMap = nonMovableCollidableHashMap,
            movableCollidableHashMap = movableCollidableHashMap,
            tick = Time.frameCount,
        };
        var tryRandomMovementJobHandle = tryRandomMovementJob.Schedule(humanMovableCount, 64, humanMovementBarrierHandle);

        var copyNativeZombieMovableGridPositionsJob = new CopyComponentData<GridPosition>
        {
            Source = zombieMovableGridPositions,
            Results = initialZombieGridPositions
        };
        var copyNativeZombieGridPositionsJobHandle = copyNativeZombieMovableGridPositionsJob.Schedule(zombieMovableCount, 2, inputDeps);

        var hashInitialZombieGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = initialZombieGridPositions,
            hashMap = initialZombieGridPositionsHashMap.ToConcurrent(),
        };
        var hashInitialZombieGridPositionsJobHandle = hashInitialZombieGridPositionsJob.Schedule(zombieMovableCount, 64, copyNativeZombieGridPositionsJobHandle);

        var zombieMovementBarrier = JobHandle.CombineDependencies(hashNonMovableCollidablePositionsJobHandle, hashMovableCollidablePositionsJobHandle, hashInitialZombieGridPositionsJobHandle);
        zombieMovementBarrier = JobHandle.CombineDependencies(zombieMovementBarrier, hashInitialHumanGridPositionsJobHandle);

        var tryFollowMovementJob = new TryFollowMovementJob
        {
            gridPositions = initialZombieGridPositions,
            nextGridPositions = updatedZombieGridPositions,
            nonMovableCollidableHashMap = nonMovableCollidableHashMap,
            movableCollidableHashMap = movableCollidableHashMap,
            targetGridPositionsHashMap = initialHumanGridPositionsHashMap,
            validTargetIndexArray = zombieTargetIndexArray,
            validTargetsArray = zombieTargetValuesArray,
            viewDistance = m_ZombieViewDistance,
        };
        var tryFollowMovementJobHandle = tryFollowMovementJob.Schedule(zombieMovableCount, 64, zombieMovementBarrier);

        var finalizeMovementBarrier = JobHandle.CombineDependencies(tryRandomMovementJobHandle, tryFollowMovementJobHandle);

        var hashHumanUpdatedGridPositionsJob = new HashGridPositionsJob
        {
            gridPositions = updatedHumanGridPositions,
            hashMap = updatedHumanGridPositionsHashMap.ToConcurrent(),
        };
        var hashHumanUpdatedGridPositionsJobHandle = hashHumanUpdatedGridPositionsJob.Schedule(humanMovableCount, 64, finalizeMovementBarrier);

        var hashZombieUpdatedGridPositionsJob = new HashGridPositionsJob
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

        return resolveCollidedHumanMovementJobHandle;
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
            typeof(Position)
        );
        m_ZombieViewDistance = 5;
    }

    protected override void OnDestroyManager()
    {
        if (m_PrevGridState.nonMovableCollidableHashMap.IsCreated)
            m_PrevGridState.nonMovableCollidableHashMap.Dispose();

        if (m_PrevGridState.nativeMovableCollidableGridPositions.IsCreated)
            m_PrevGridState.nativeMovableCollidableGridPositions.Dispose();
        if (m_PrevGridState.movableCollidableHashMap.IsCreated)
            m_PrevGridState.movableCollidableHashMap.Dispose();

        if (m_PrevGridState.initialHumanGridPositions.IsCreated)
            m_PrevGridState.initialHumanGridPositions.Dispose();
        if (m_PrevGridState.initialHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialHumanGridPositionsHashMap.Dispose();
        if (m_PrevGridState.updatedHumanGridPositions.IsCreated)
            m_PrevGridState.updatedHumanGridPositions.Dispose();
        if (m_PrevGridState.updatedHumanGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedHumanGridPositionsHashMap.Dispose();

        if (m_PrevGridState.initialZombieGridPositions.IsCreated)
            m_PrevGridState.initialZombieGridPositions.Dispose();
        if (m_PrevGridState.initialZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.initialZombieGridPositionsHashMap.Dispose();
        if (m_PrevGridState.zombieTargetIndexArray.IsCreated)
            m_PrevGridState.zombieTargetIndexArray.Dispose();
        if (m_PrevGridState.zombieTargetValuesArray.IsCreated)
            m_PrevGridState.zombieTargetValuesArray.Dispose();
        if (m_PrevGridState.updatedZombieGridPositions.IsCreated)
            m_PrevGridState.updatedZombieGridPositions.Dispose();
        if (m_PrevGridState.updatedZombieGridPositionsHashMap.IsCreated)
            m_PrevGridState.updatedZombieGridPositionsHashMap.Dispose();
    }
}
