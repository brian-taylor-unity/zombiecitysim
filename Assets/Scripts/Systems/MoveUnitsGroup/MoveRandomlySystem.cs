﻿using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Random = Unity.Mathematics.Random;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public class MoveRandomlySystem : JobComponentSystem
{
    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        inputDeps = JobHandle.CombineDependencies(inputDeps,
            World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableJobHandle,
            World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableJobHandle
        );

        var staticCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_StaticCollidableHashMap;
        var dynamicCollidableHashMap = World.GetExistingSystem<HashCollidablesSystem>().m_DynamicCollidableHashMap;

        var tick = UnityEngine.Time.frameCount;
        var moveRandomlyJobHandle = Entities
            .WithName("MoveRandomly")
            .WithAll<MoveRandomly>()
            .WithChangeFilter<TurnsUntilActive>()
            .WithReadOnly(staticCollidableHashMap)
            .WithReadOnly(dynamicCollidableHashMap)
            .WithBurst()
            .ForEach((int entityInQueryIndex, ref NextGridPosition nextGridPosition, in GridPosition gridPosition, in TurnsUntilActive turnsUntilActive) =>
                {
                    if (turnsUntilActive.Value != 0)
                        return;

                    int3 myGridPositionValue = gridPosition.Value;

                    int upDirKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
                    int rightDirKey = (int)math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
                    int downDirKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
                    int leftDirKey = (int)math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));

                    bool upMoveAvail = true;
                    bool rightMoveAvail = true;
                    bool downMoveAvail = true;
                    bool leftMoveAvail = true;

                    if (staticCollidableHashMap.TryGetValue(upDirKey, out _) || dynamicCollidableHashMap.TryGetValue(upDirKey, out _))
                        upMoveAvail = false;
                    if (staticCollidableHashMap.TryGetValue(rightDirKey, out _) || dynamicCollidableHashMap.TryGetValue(rightDirKey, out _))
                        rightMoveAvail = false;
                    if (staticCollidableHashMap.TryGetValue(downDirKey, out _) || dynamicCollidableHashMap.TryGetValue(downDirKey, out _))
                        downMoveAvail = false;
                    if (staticCollidableHashMap.TryGetValue(leftDirKey, out _) || dynamicCollidableHashMap.TryGetValue(leftDirKey, out _))
                        leftMoveAvail = false;

                    // Pick a random direction to move
                    uint seed = (uint)(tick * (int)math.hash(myGridPositionValue) * entityInQueryIndex);
                    if (seed == 0)
                        seed += (uint)(tick + entityInQueryIndex);

                    Random rand = new Random(seed);
                    int randomDirIndex = rand.NextInt(0, 4);

                    bool moved = false;
                    for (int i = 0; i < 4 && !moved; i++)
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
                    }
                    nextGridPosition = new NextGridPosition { Value = myGridPositionValue };
                })
            .Schedule(inputDeps);

        return moveRandomlyJobHandle;
    }
}
