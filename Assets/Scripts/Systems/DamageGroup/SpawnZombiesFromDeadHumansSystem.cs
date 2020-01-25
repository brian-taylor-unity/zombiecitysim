//using Unity.Collections;
//using Unity.Entities;

//[UpdateInGroup(typeof(DamageGroup))]
//[UpdateAfter(typeof(DamageSystem))]
//public class SpawnZombiesFromDeadHumansSystem : ComponentSystem
//{
//    private EntityQuery m_HumansGroup;

//    protected override void OnUpdate()
//    {
//        var gridPositionArray = m_HumansGroup.ToComponentDataArray<GridPosition>(Allocator.TempJob);
//        var healthArray = m_HumansGroup.ToComponentDataArray<Health>(Allocator.TempJob);

//        for (int i = 0; i < healthArray.Length; i++)
//        {
//            if (healthArray[i].Value <= 0)
//            {
//                Bootstrap.AddZombieCharacter(gridPositionArray[i].Value.x, gridPositionArray[i].Value.z,
//                                             GameController.instance.zombieStartingHealth, GameController.instance.zombieDamage,
//                                             GameController.instance.zombieTurnDelay);
//            }
//        }

//        gridPositionArray.Dispose();
//        healthArray.Dispose();
//    }

//    protected override void OnCreate()
//    {
//        m_HumansGroup = GetEntityQuery(
//            ComponentType.ReadOnly(typeof(Human)),
//            ComponentType.ReadOnly(typeof(GridPosition)),
//            ComponentType.ReadOnly(typeof(Health))
//        );
//    }
//}
