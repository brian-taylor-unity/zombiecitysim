using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class HumanCreator
{
    public static void CreateHuman(EntityCommandBuffer.ParallelWriter commandBuffer, int index, Entity prefab, int3 gridPosition, int health, int damage, int turnsUntilActive, uint randomSeed)
    {
        var instance = commandBuffer.Instantiate(index, prefab);
        commandBuffer.SetComponent(index, instance, new Translation { Value = new float3(gridPosition) });
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new NextGridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Health { Value = health });
        commandBuffer.AddComponent(index, instance, new Damage { Value = damage });
        commandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = turnsUntilActive });
        commandBuffer.AddComponent(index, instance, new Human());
        commandBuffer.AddComponent(index, instance, new DynamicCollidable());
        commandBuffer.AddComponent(index, instance, new FollowTarget());
        commandBuffer.AddComponent(index, instance, new LineOfSight());
        commandBuffer.AddComponent(index, instance, new CharacterColor { Value = new float4(0.0f, 1.0f, 0.0f, turnsUntilActive == 1 ? 1.0f : 0.85f) });
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }
}