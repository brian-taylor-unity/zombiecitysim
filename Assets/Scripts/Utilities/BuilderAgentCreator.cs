using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public static class BuilderAgentCreator
{
    [BurstCompile]
    public static void CreateHighwayBuilderAgent(ref EntityCommandBuffer.ParallelWriter commandBuffer, int index, [ReadOnly] in int3 gridPosition, [ReadOnly] in int3 direction, int lifetime, uint randomSeed)
    {
        var instance = commandBuffer.CreateEntity(index);
        commandBuffer.AddComponent(index, instance, new HighwayBuilderAgent());
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Direction { Value = direction });
        commandBuffer.AddComponent(index, instance, new BuilderLifetime { Value = lifetime });
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }

    [BurstCompile]
    public static void CreateCityStreetBuilderAgent(ref EntityCommandBuffer.ParallelWriter commandBuffer, int index, [ReadOnly] in int3 gridPosition, [ReadOnly] in int3 direction, int lifetime, uint randomSeed)
    {
        var instance = commandBuffer.CreateEntity(index);
        commandBuffer.AddComponent(index, instance, new CityStreetBuilderAgent());
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Direction { Value = direction });
        commandBuffer.AddComponent(index, instance, new BuilderLifetime { Value = lifetime });
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }
}
