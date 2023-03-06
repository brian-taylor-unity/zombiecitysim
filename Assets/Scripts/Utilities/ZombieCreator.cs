using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

public static class ZombieCreator
{
    public static void CreateZombie(EntityCommandBuffer.ParallelWriter commandBuffer, int index, Entity prefab, int3 gridPosition, int health, int damage, int turnsUntilActive, uint randomSeed)
    {
        var instance = commandBuffer.Instantiate(index, prefab);
        commandBuffer.SetComponent(index, instance, LocalTransform.FromPosition(gridPosition));
        commandBuffer.AddComponent(index, instance, new GridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new NextGridPosition { Value = gridPosition });
        commandBuffer.AddComponent(index, instance, new Health { Value = health });
        commandBuffer.AddComponent(index, instance, new Damage { Value = damage });
        commandBuffer.AddComponent(index, instance, new TurnsUntilActive { Value = turnsUntilActive });
        commandBuffer.AddComponent(index, instance, new Zombie());
        commandBuffer.AddComponent(index, instance, new DynamicCollidable());
        commandBuffer.AddComponent(index, instance, new MoveTowardsTarget());
        commandBuffer.AddComponent(index, instance, new MoveEscapeTarget());
        commandBuffer.AddComponent(index, instance, new CharacterColor { Value = new float4(1.0f, 0.0f, 0.0f, turnsUntilActive == 1 ? 1.0f : 0.85f) });
        commandBuffer.AddComponent(index, instance, new RandomGenerator { Value = new Random(randomSeed) });
    }

    public static void CreateZombie(ref SystemState state, Entity prefab, int3 gridPosition, int health, int damage, int turnsUntilActive, uint randomSeed)
    {
        var instance = state.EntityManager.Instantiate(prefab);
        state.EntityManager.SetComponentData(instance, LocalTransform.FromPosition(gridPosition));
        state.EntityManager.AddComponentData(instance, new GridPosition { Value = gridPosition });
        state.EntityManager.AddComponentData(instance, new NextGridPosition { Value = gridPosition });
        state.EntityManager.AddComponentData(instance, new Health { Value = health });
        state.EntityManager.AddComponentData(instance, new Damage { Value = damage });
        state.EntityManager.AddComponentData(instance, new TurnsUntilActive { Value = turnsUntilActive });
        state.EntityManager.AddComponentData(instance, new Zombie());
        state.EntityManager.AddComponentData(instance, new DynamicCollidable());
        state.EntityManager.AddComponentData(instance, new MoveTowardsTarget());
        state.EntityManager.AddComponentData(instance, new MoveEscapeTarget());
        state.EntityManager.AddComponentData(instance, new CharacterColor { Value = new float4(1.0f, 0.0f, 0.0f, turnsUntilActive == 1 ? 1.0f : 0.85f) });
        state.EntityManager.AddComponentData(instance, new RandomGenerator { Value = new Random(randomSeed) });
    }
}
