using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
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
        [ReadOnly] public ComponentDataArray<GridPosition> collidableGridPositions;
        public NativeHashMap<int, int>.Concurrent hashMap;

        public void Execute(int index)
        {
            var hash = GridHash.Hash(collidableGridPositions[index].Value);
            hashMap.TryAdd(hash, index);
        }
    }

    //[BurstCompile]
    struct MovementJob : IJobProcessComponentData<Position, GridPosition, Movable>
    {
        [ReadOnly] public NativeHashMap<int, int> hashMap;

        public void Execute(ref Position position, ref GridPosition gridPosition, [ReadOnly] ref Movable moveable)
        {
            float3 myPosition = position.Value;
            int3 myGridPostion = gridPosition.Value;

            int upDirKey = GridHash.Hash(new int3(myGridPostion.x, myGridPostion.y, myGridPostion.z + 1));
            int rightDirKey = GridHash.Hash(new int3(myGridPostion.x + 1, myGridPostion.y, myGridPostion.z));
            int downDirKey = GridHash.Hash(new int3(myGridPostion.x, myGridPostion.y, myGridPostion.z - 1));
            int leftDirKey = GridHash.Hash(new int3(myGridPostion.x - 1, myGridPostion.y, myGridPostion.z));

            bool upMoveAvail = true;
            bool rightMoveAvail = true;
            bool downMoveAvail = true;
            bool leftMoveAvail = true;

            if (hashMap.TryGetValue(upDirKey, out _))
                upMoveAvail = false;
            if (hashMap.TryGetValue(rightDirKey, out _))
                rightMoveAvail = false;
            if (hashMap.TryGetValue(downDirKey, out _))
                downMoveAvail = false;
            if (hashMap.TryGetValue(leftDirKey, out _))
                leftMoveAvail = false;

            if (upMoveAvail)
                UnityEngine.Debug.Log("upMoveAvail");
            if (rightMoveAvail)
                UnityEngine.Debug.Log("rightMoveAvail");
            if (downMoveAvail)
                UnityEngine.Debug.Log("downMoveAvail");
            if (leftMoveAvail)
                UnityEngine.Debug.Log("leftMoveAvail");

            // Pick a random direction to move
            Random rand = new Random((uint)GridHash.Hash(myGridPostion));
            int randomDirIndex = rand.NextInt(0, 4);
            UnityEngine.Debug.Log(randomDirIndex);

            bool moved = false;
            for (int i = 0; i < 4; i++)
            {
                int direction = (randomDirIndex + i) % 4;
                switch (direction)
                {
                    case 0:
                        if (upMoveAvail)
                        {
                            myPosition.z += 1f;
                            myGridPostion.z += 1;
                            moved = true;
                        }
                        break;
                    case 1:
                        if (rightMoveAvail)
                        {
                            myPosition.x += 1f;
                            myGridPostion.x += 1;
                            moved = true;
                        }
                        break;
                    case 2:
                        if (downMoveAvail)
                        {
                            myPosition.z -= 1f;
                            myGridPostion.z -= 1;
                            moved = true;
                        }
                        break;
                    case 3:
                        if (leftMoveAvail)
                        {
                            myPosition.x -= 1f;
                            myGridPostion.x -= 1;
                            moved = true;
                        }
                        break;
                }

                if (moved)
                {
                    position.Value = myPosition;
                    gridPosition.Value = myGridPostion;
                    break;
                }
            }
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
        var collidableGridPositions = m_CollidableGroup.GetComponentDataArray<GridPosition>();
        var collidableCount = collidableGridPositions.Length;
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
            collidableGridPositions = collidableGridPositions,
            hashMap = hashMap.ToConcurrent()
        };
        var hashCollidablePositionsJobHandle = hashCollidablePositionsJob.Schedule(collidableCount, 64, inputDeps);

        var movementJob = new MovementJob
        {
            hashMap = hashMap,
        };
        var movementJobHandle = movementJob.Schedule(this, hashCollidablePositionsJobHandle);

        return movementJobHandle;
    }
}
