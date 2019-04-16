using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(DamageSystem))]
public class SpawnZombiesFromDeadHumansSystem : ComponentSystem
{
    private EntityQuery m_HumansGroup;

    protected override void OnUpdate()
    {
        var gridPositionArray = m_HumansGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var healthArray = m_HumansGroup.ToComponentDataArray<Health>(Allocator.TempJob);

        var manager = PostUpdateCommands;
        for (int i = 0; i < healthArray.Length; i++)
        {
            if (healthArray[i].Value <= 0)
            {
                Entity entity = manager.CreateEntity(Bootstrap.ZombieArchetype);
                manager.SetComponent(entity, new GridPosition { Value = gridPositionArray[i].Value });
                manager.SetComponent(entity, new Translation { Value = new float3(gridPositionArray[i].Value) });
                manager.SetComponent(entity, new Health { Value = Bootstrap.ZombieStartingHealth });
                manager.SetComponent(entity, new Damage { Value = Bootstrap.ZombieDamage });
                manager.AddSharedComponent(entity, Bootstrap.ZombieMeshInstanceRenderer);
            }
        }

        gridPositionArray.Dispose();
        healthArray.Dispose();
    }

    protected override void OnCreate()
    {
        m_HumansGroup = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(GridPosition)),
            ComponentType.ReadOnly(typeof(Health))
        );
    }
}
