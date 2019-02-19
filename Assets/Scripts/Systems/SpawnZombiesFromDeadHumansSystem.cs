using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(DamageSystem))]
public class SpawnZombiesFromDeadHumansSystem : ComponentSystem
{
    private ComponentGroup m_HumansGroup;

    protected override void OnUpdate()
    {
        var gridPositionArray = m_HumansGroup.GetComponentDataArray<GridPosition>();
        var healthArray = m_HumansGroup.GetComponentDataArray<Health>();

        var manager = PostUpdateCommands;
        for (int i = 0; i < healthArray.Length; i++)
        {
            if (healthArray[i].Value <= 0)
            {
                Entity entity = manager.CreateEntity(Bootstrap.ZombieArchetype);
                manager.SetComponent(entity, new GridPosition { Value = gridPositionArray[i].Value });
                manager.SetComponent(entity, new Position { Value = new float3(gridPositionArray[i].Value) });
                manager.SetComponent(entity, new Health { Value = Bootstrap.ZombieStartingHealth });
                manager.SetComponent(entity, new Damage { Value = Bootstrap.ZombieDamage });
                manager.AddSharedComponent(entity, Bootstrap.ZombieMeshInstanceRenderer);
            }
        }
    }

    protected override void OnCreateManager()
    {
        m_HumansGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Health))
        );
    }
}
