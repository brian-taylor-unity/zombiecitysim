using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(MoveFollowTargetSystem))]
public class SpawnZombiesFromDeadHumansSystem : ComponentSystem
{
    private EntityQuery m_HumansGroup;

    protected override void OnUpdate()
    {
        var gridPositionArray = m_HumansGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
        var healthArray = m_HumansGroup.ToComponentDataArray<Health>(Allocator.TempJob);

        for (int i = 0; i < healthArray.Length; i++)
        {
            if (healthArray[i].Value <= 0)
            {
                Entity entity = EntityManager.CreateEntity(Bootstrap.ZombieArchetype);
                EntityManager.SetComponentData(entity, new GridPosition { Value = gridPositionArray[i].Value });
                EntityManager.SetComponentData(entity, new Translation { Value = new float3(gridPositionArray[i].Value) });
                EntityManager.SetComponentData(entity, new Health { Value = Bootstrap.ZombieStartingHealth });
                EntityManager.SetComponentData(entity, new Damage { Value = Bootstrap.ZombieDamage });
                EntityManager.SetComponentData(entity, new TurnsUntilMove { Value = Bootstrap.ZombieTurnDelay });
                EntityManager.AddSharedComponentData(entity, Bootstrap.ZombieMeshInstanceRenderer);
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
