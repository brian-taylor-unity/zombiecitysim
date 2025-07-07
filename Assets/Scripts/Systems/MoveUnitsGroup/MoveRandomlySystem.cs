using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct MoveRandomlyJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<uint, int> StaticCollidableHashMap;
    [ReadOnly] public NativeParallelHashMap<uint, int> DynamicCollidableHashMap;

    public void Execute(ref DesiredNextGridPosition desiredNextGridPosition, ref RandomGenerator random, [ReadOnly] in GridPosition gridPosition)
    {
        var myGridPositionValue = gridPosition.Value;

        var upDirKey = math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
        var rightDirKey = math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
        var downDirKey = math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
        var leftDirKey = math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));

        var upMoveAvail = true;
        var rightMoveAvail = true;
        var downMoveAvail = true;
        var leftMoveAvail = true;

        if (StaticCollidableHashMap.TryGetValue(upDirKey, out _) || DynamicCollidableHashMap.TryGetValue(upDirKey, out _))
            upMoveAvail = false;
        if (StaticCollidableHashMap.TryGetValue(rightDirKey, out _) || DynamicCollidableHashMap.TryGetValue(rightDirKey, out _))
            rightMoveAvail = false;
        if (StaticCollidableHashMap.TryGetValue(downDirKey, out _) || DynamicCollidableHashMap.TryGetValue(downDirKey, out _))
            downMoveAvail = false;
        if (StaticCollidableHashMap.TryGetValue(leftDirKey, out _) || DynamicCollidableHashMap.TryGetValue(leftDirKey, out _))
            leftMoveAvail = false;

        var randomDirIndex = random.Value.NextInt(0, 4);
        var moved = false;
        for (var i = 0; i < 4 && !moved; i++)
        {
            var direction = (randomDirIndex + i) % 4;
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
        desiredNextGridPosition = new DesiredNextGridPosition { Value = myGridPositionValue };
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsHumansSystem))]
[RequireMatchingQueriesForUpdate]
public partial struct MoveRandomlySystem : ISystem
{
    private EntityQuery _moveRandomlyQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _moveRandomlyQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<MoveRandomly, TurnActive>());

        state.RequireForUpdate<RunWorld>();
        state.RequireForUpdate<HashStaticCollidableSystemComponent>();
        state.RequireForUpdate<HashDynamicCollidableSystemComponent>();
        state.RequireForUpdate(_moveRandomlyQuery);
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var staticCollidableComponent = SystemAPI.GetSingleton<HashStaticCollidableSystemComponent>();
        var dynamicCollidableComponent = SystemAPI.GetSingleton<HashDynamicCollidableSystemComponent>();

        state.Dependency = JobHandle.CombineDependencies(state.Dependency, staticCollidableComponent.Handle, dynamicCollidableComponent.Handle);

        var staticCollidableHashMap = staticCollidableComponent.HashMap;
        var dynamicCollidableHashMap = dynamicCollidableComponent.HashMap;

        if (!staticCollidableHashMap.IsCreated || !dynamicCollidableHashMap.IsCreated)
            return;

        state.Dependency = new MoveRandomlyJob
        {
            StaticCollidableHashMap = staticCollidableHashMap,
            DynamicCollidableHashMap = dynamicCollidableHashMap
        }.ScheduleParallel(_moveRandomlyQuery, state.Dependency);
    }
}
