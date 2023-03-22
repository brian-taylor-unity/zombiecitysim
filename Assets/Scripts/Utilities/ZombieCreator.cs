using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Random = Unity.Mathematics.Random;

public static class ZombieCreator
{
    [BurstCompile]
    public static float4 GetFullHealthColor()
    {
        return new float4(1.0f, 0.0f, 0.0f, 1.0f);
    }

    [BurstCompile]
    public static void CreateZombie(EntityCommandBuffer.ParallelWriter commandBuffer, int index, Entity prefab, int3 gridPosition, int health, int damage, int turnsUntilActive, uint randomSeed)
    {
        var instance = commandBuffer.Instantiate(index, prefab);
        commandBuffer.SetComponent(index, instance, LocalTransform.FromPosition(gridPosition));
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new NextGridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Health { Value = health });
        commandBuffer.AddComponent(index, instance, new MaxHealth { Value = health });
        commandBuffer.AddComponent(index, instance, new Damage { Value = damage });
        commandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = turnsUntilActive });
        commandBuffer.AddComponent<TurnActive>(index, instance);
        commandBuffer.SetComponentEnabled<TurnActive>(index, instance, false);
        commandBuffer.AddComponent(index, instance, new Zombie());
        commandBuffer.AddComponent(index, instance, new DynamicCollidable());
        commandBuffer.AddComponent(index, instance, new MoveTowardsHuman());
        var healthColor = GetFullHealthColor();
        healthColor.w = turnsUntilActive == 1 ? 1.0f : 0.85f;
        commandBuffer.AddComponent(index, instance, new CharacterColor { Value = healthColor });
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }
}
