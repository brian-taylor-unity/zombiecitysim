using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsTargetSystem))]
public partial class MoveRandomlySystem : SystemBase
{
    private EntityQuery _moveRandomlyQuery;

    protected override void OnCreate()
    {
        RequireForUpdate<StaticCollidableComponent>();
        RequireForUpdate<DynamicCollidableComponent>();
        RequireAnyForUpdate(_moveRandomlyQuery);
    }

    protected override void OnUpdate()
    {
        var staticCollidableComponent = SystemAPI.GetSingleton<StaticCollidableComponent>();
        var dynamicCollidableComponent = SystemAPI.GetSingleton<DynamicCollidableComponent>();

        Dependency = JobHandle.CombineDependencies(
            Dependency,
            staticCollidableComponent.Handle,
            dynamicCollidableComponent.Handle
        );

        var staticCollidableHashMap = staticCollidableComponent.HashMap;
        var dynamicCollidableHashMap = dynamicCollidableComponent.HashMap;

        if (!staticCollidableHashMap.IsCreated || !dynamicCollidableHashMap.IsCreated)
            return;

        Entities
            .WithName("MoveRandomly")
            .WithAll<MoveRandomly>()
            .WithStoreEntityQueryInField(ref _moveRandomlyQuery)
            .WithChangeFilter<TurnsUntilActive>()
            .WithReadOnly(staticCollidableHashMap)
            .WithReadOnly(dynamicCollidableHashMap)
            .WithBurst()
            .ForEach((int entityInQueryIndex, ref NextGridPosition nextGridPosition, ref RandomGenerator random, in GridPosition gridPosition, in TurnsUntilActive turnsUntilActive) =>
                {
                    if (turnsUntilActive.Value != 1)
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

                    int randomDirIndex = random.Value.NextInt(0, 4);
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
            .ScheduleParallel();
    }
}
