using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

[UpdateAfter(typeof(MoveTowardsTargetSystem))]
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
                //Entity entity = EntityManager.CreateEntity(Bootstrap.ZombieArchetype);
                //EntityManager.SetComponentData(entity, new Translation { Value = new float3(gridPositionArray[i].Value) });
                //EntityManager.SetComponentData(entity, new GridPosition { Value = gridPositionArray[i].Value });
                //EntityManager.SetComponentData(entity, new NextGridPosition { Value = gridPositionArray[i].Value });
                //EntityManager.SetComponentData(entity, new Health { Value = GameController.instance.zombieStartingHealth });
                //EntityManager.SetComponentData(entity, new Damage { Value = GameController.instance.zombieDamage });
                //EntityManager.SetComponentData(entity, new TurnsUntilMove { Value = GameController.instance.zombieTurnDelay });
                //EntityManager.AddSharedComponentData(entity, Bootstrap.ZombieMeshInstanceRenderer);
                Bootstrap.AddZombieCharacter(gridPositionArray[i].Value.x, gridPositionArray[i].Value.z,
                    GameController.instance.zombieStartingHealth, GameController.instance.zombieDamage, GameController.instance.zombieTurnDelay);
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
