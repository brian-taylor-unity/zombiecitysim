using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;
using Unity.Transforms;

[BurstCompile]
public static class TileCreator
{
    [BurstCompile]
    public static void CreateRoadTile(ref EntityCommandBuffer.ParallelWriter commandBuffer, int index,
        [ReadOnly] in Entity prefab, [ReadOnly] in int3 gridPosition)
    {
        var instance = commandBuffer.Instantiate(index, prefab);
        commandBuffer.AddComponent(index, instance, new Road());
        commandBuffer.SetComponent(index, instance, LocalTransform.FromPositionRotation(gridPosition, quaternion.RotateX(math.PIHALF)));
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new URPMaterialPropertyBaseColor { Value = new float4(0.8f, 0.8f, 0.8f, 1.0f) });
    }

    [BurstCompile]
    public static void CreateHuman(ref EntityCommandBuffer.ParallelWriter commandBuffer, int index,
        [ReadOnly] in Entity prefab, [ReadOnly] in int3 gridPosition, ref float4 fullHealthColor, int health, int damage,
        int turnsUntilActive, uint randomSeed)
    {
        var instance = commandBuffer.Instantiate(index, prefab);
        commandBuffer.SetComponent(index, instance, LocalTransform.FromPosition(gridPosition));
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new DesiredNextGridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Health { Value = health });
        commandBuffer.AddComponent(index, instance, new MaxHealth { Value = health });
        fullHealthColor.w = turnsUntilActive == 1 ? 1.0f : 0.85f;
        commandBuffer.AddComponent(index, instance, new URPMaterialPropertyBaseColor { Value = fullHealthColor });
        commandBuffer.AddComponent<Dead>(index, instance);
        commandBuffer.SetComponentEnabled<Dead>(index, instance, false);
        commandBuffer.AddComponent(index, instance, new Damage { Value = damage });
        commandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = turnsUntilActive });
        commandBuffer.AddComponent<TurnActive>(index, instance);
        commandBuffer.SetComponentEnabled<TurnActive>(index, instance, false);
        commandBuffer.AddComponent(index, instance, new Human());
        commandBuffer.AddComponent(index, instance, new DynamicCollidable());
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }

    [BurstCompile]
    public static void CreateZombie(ref EntityCommandBuffer.ParallelWriter commandBuffer, int index,
        [ReadOnly] in Entity prefab, [ReadOnly] in int3 gridPosition, ref float4 fullHealthColor, int health, int damage,
        int turnsUntilActive, uint randomSeed)
    {
        var instance = commandBuffer.Instantiate(index, prefab);
        commandBuffer.SetComponent(index, instance, LocalTransform.FromPosition(gridPosition));
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new DesiredNextGridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Health { Value = health });
        commandBuffer.AddComponent(index, instance, new MaxHealth { Value = health });
        fullHealthColor.w = turnsUntilActive == 1 ? 1.0f : 0.85f;
        commandBuffer.AddComponent(index, instance, new URPMaterialPropertyBaseColor { Value = fullHealthColor });
        commandBuffer.AddComponent<Dead>(index, instance);
        commandBuffer.SetComponentEnabled<Dead>(index, instance, false);
        commandBuffer.AddComponent(index, instance, new Damage { Value = damage });
        commandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = turnsUntilActive });
        commandBuffer.AddComponent<TurnActive>(index, instance);
        commandBuffer.SetComponentEnabled<TurnActive>(index, instance, false);
        commandBuffer.AddComponent(index, instance, new Zombie());
        commandBuffer.AddComponent(index, instance, new DynamicCollidable());
        commandBuffer.AddComponent(index, instance, new MoveTowardsHuman());
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }
}