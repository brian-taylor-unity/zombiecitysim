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
                manager.CreateEntity(Bootstrap.ZombieArchetype);
                manager.SetComponent(new GridPosition { Value = gridPositionArray[i].Value });
                manager.SetComponent(new Position { Value = new float3(gridPositionArray[i].Value) });
                manager.SetComponent(new Health { Value = Bootstrap.ZombieStartingHealth });
                manager.SetComponent(new Damage { Value = Bootstrap.ZombieDamage });
                manager.AddSharedComponent(Bootstrap.ZombieMeshInstanceRenderer);
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
