using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateAfter(typeof(MoveTowardsTargetSystem))]
public class MoveRandomlySystem : JobComponentSystem
{
    private EntityQuery m_MoveRandomlyGroup;

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

    [BurstCompile]
    struct MoveRandomlyJob : IJobForEachWithEntity<GridPosition, NextGridPosition, TurnsUntilActive>
    {
        [ReadOnly] public NativeMultiHashMap<int, int> staticCollidableHashMap;
        [ReadOnly] public NativeMultiHashMap<int, int> dynamicCollidableHashMap;
        [ReadOnly] public int tick;

        public void Execute(Entity entity, int index, [ReadOnly] ref GridPosition gridPosition, ref NextGridPosition nextGridPosition, [ReadOnly] ref TurnsUntilActive turnsUntilActive)
        {
            if (turnsUntilActive.Value != 0)
                return;

            int3 myGridPositionValue = gridPosition.Value;

            int upDirKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
            int rightDirKey = GridHash.Hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
            int downDirKey = GridHash.Hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
            int leftDirKey = GridHash.Hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));

            bool upMoveAvail = true;
            bool rightMoveAvail = true;
            bool downMoveAvail = true;
            bool leftMoveAvail = true;

            if (staticCollidableHashMap.TryGetFirstValue(upDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(upDirKey, out _, out _))
                upMoveAvail = false;
            if (staticCollidableHashMap.TryGetFirstValue(rightDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(rightDirKey, out _, out _))
                rightMoveAvail = false;
            if (staticCollidableHashMap.TryGetFirstValue(downDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(downDirKey, out _, out _))
                downMoveAvail = false;
            if (staticCollidableHashMap.TryGetFirstValue(leftDirKey, out _, out _) || dynamicCollidableHashMap.TryGetFirstValue(leftDirKey, out _, out _))
                leftMoveAvail = false;

            // Pick a random direction to move
            uint seed = (uint)(tick * GridHash.Hash(myGridPositionValue) * index);
            if (seed == 0)
                seed += (uint)(tick + index);

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
            nextGridPosition = new NextGridPosition { Value = myGridPositionValue };
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var staticCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMap;
        var dynamicCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMap;

        var moveRandomlyJob = new MoveRandomlyJob
        {
            staticCollidableHashMap = staticCollidableHashMap,
            dynamicCollidableHashMap = dynamicCollidableHashMap,
            tick = UnityEngine.Time.frameCount,
        };
        var moveRandomlyJobHandle = moveRandomlyJob.Schedule(m_MoveRandomlyGroup, inputDeps);

        return moveRandomlyJobHandle;
    }
    protected override void OnCreate()
    {
        m_MoveRandomlyGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(MoveRandomly)),
            ComponentType.ReadOnly(typeof(TurnsUntilActive)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            typeof(NextGridPosition)
        );
    }
}
