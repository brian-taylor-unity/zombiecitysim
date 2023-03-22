using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[BurstCompile]
public partial struct MoveRandomlyJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int, int> StaticCollidableHashMap;
    [ReadOnly] public NativeParallelHashMap<int, int> DynamicCollidableHashMap;

    public void Execute(ref NextGridPosition nextGridPosition, ref RandomGenerator random, in GridPosition gridPosition)
    {
        int3 myGridPositionValue = gridPosition.Value;

        int upDirKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z + 1));
        int rightDirKey = (int)math.hash(new int3(myGridPositionValue.x + 1, myGridPositionValue.y, myGridPositionValue.z));
        int downDirKey = (int)math.hash(new int3(myGridPositionValue.x, myGridPositionValue.y, myGridPositionValue.z - 1));
        int leftDirKey = (int)math.hash(new int3(myGridPositionValue.x - 1, myGridPositionValue.y, myGridPositionValue.z));

        bool upMoveAvail = true;
        bool rightMoveAvail = true;
        bool downMoveAvail = true;
        bool leftMoveAvail = true;

        if (StaticCollidableHashMap.TryGetValue(upDirKey, out _) || DynamicCollidableHashMap.TryGetValue(upDirKey, out _))
            upMoveAvail = false;
        if (StaticCollidableHashMap.TryGetValue(rightDirKey, out _) || DynamicCollidableHashMap.TryGetValue(rightDirKey, out _))
            rightMoveAvail = false;
        if (StaticCollidableHashMap.TryGetValue(downDirKey, out _) || DynamicCollidableHashMap.TryGetValue(downDirKey, out _))
            downMoveAvail = false;
        if (StaticCollidableHashMap.TryGetValue(leftDirKey, out _) || DynamicCollidableHashMap.TryGetValue(leftDirKey, out _))
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
    }
}

[UpdateInGroup(typeof(MoveUnitsGroup))]
[UpdateBefore(typeof(MoveTowardsHumansSystem))]
public partial struct MoveRandomlySystem : ISystem
{
    private EntityQuery _moveRandomlyQuery;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _moveRandomlyQuery = state.GetEntityQuery(new EntityQueryBuilder(Allocator.Temp).WithAll<MoveRandomly, TurnActive>());

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
