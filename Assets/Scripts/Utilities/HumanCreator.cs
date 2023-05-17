using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[BurstCompile]
public static class HumanCreator
{
    [BurstCompile]
    public static void FillFullHealthColor(ref float4 healthColor)
    {
        healthColor.x = 0.0f;
        healthColor.y = 1.0f;
        healthColor.z = 0.0f;
        healthColor.w = 1.0f;
    }

    [BurstCompile]
    public static void CreateHuman(ref EntityCommandBuffer.ParallelWriter commandBuffer, int index, [ReadOnly] in Entity prefab, [ReadOnly] in int3 gridPosition, int health, int damage, int turnsUntilActive, uint randomSeed)
    {
        var instance = commandBuffer.Instantiate(index, prefab);
        commandBuffer.SetComponent(index, instance, LocalTransform.FromPosition(gridPosition));
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new DesiredNextGridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Health { Value = health });
        commandBuffer.AddComponent(index, instance, new MaxHealth { Value = health });
        commandBuffer.AddComponent<Dead>(index, instance);
        commandBuffer.SetComponentEnabled<Dead>(index, instance, false);
        commandBuffer.AddComponent(index, instance, new Damage { Value = damage });
        commandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = turnsUntilActive });
        commandBuffer.AddComponent<TurnActive>(index, instance);
        commandBuffer.SetComponentEnabled<TurnActive>(index, instance, false);
        commandBuffer.AddComponent(index, instance, new Human());
        commandBuffer.AddComponent(index, instance, new DynamicCollidable());
        commandBuffer.AddComponent(index, instance, new LineOfSight());
        var healthColor = new float4();
        FillFullHealthColor(ref healthColor);
        healthColor.w = turnsUntilActive == 1 ? 1.0f : 0.85f;
        commandBuffer.AddComponent(index, instance, new CharacterColor { Value = healthColor });
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }
}
