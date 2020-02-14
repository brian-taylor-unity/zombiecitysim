//using Unity.Entities;
//using Unity.Jobs;
//using Unity.Rendering;

//[UpdateInGroup(typeof(EndGroup))]
//public class UpdateRenderMeshSystem : JobComponentSystem
//{
//    private EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystem;

//    protected override void OnCreate()
//    {
//        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
//    }

//    protected override JobHandle OnUpdate(JobHandle inputDeps)
//    {
//        var Commands = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();

//        var updateHumanRenderMeshJobHandle = Entities
//            .WithName("UpdateHumanRenderMesh")
//            .WithAll<Human>()
//            .WithChangeFilter<HealthRange>()
//            .WithoutBurst()
//            .ForEach((int entityInQueryIndex, Entity entity, in HealthRange healthRange) =>
//                {
//                    if (healthRange.Value == 75)
//                    {
//                        Commands.RemoveComponent(entityInQueryIndex, entity, typeof(RenderMesh));
//                        Commands.AddSharedComponent(entityInQueryIndex, entity, GameController.instance.Human_75_RenderMesh);
//                    }
//                    if (healthRange.Value == 50)
//                    {
//                        Commands.RemoveComponent(entityInQueryIndex, entity, typeof(RenderMesh));
//                        Commands.AddSharedComponent(entityInQueryIndex, entity, GameController.instance.Human_50_RenderMesh);
//                    }
//                    if (healthRange.Value == 25)
//                    {
//                        Commands.RemoveComponent(entityInQueryIndex, entity, typeof(RenderMesh));
//                        Commands.AddSharedComponent(entityInQueryIndex, entity, GameController.instance.Human_25_RenderMesh);
//                    }
//                })
//            .Schedule(inputDeps);
//        m_EntityCommandBufferSystem.AddJobHandleForProducer(updateHumanRenderMeshJobHandle);

//        var updateZombieRenderMeshJobHandle = Entities
//            .WithName("UpdateZombieRenderMesh")
//            .WithAll<Zombie>()
//            .WithChangeFilter<HealthRange>()
//            .WithoutBurst()
//            .ForEach((int entityInQueryIndex, Entity entity, in HealthRange healthRange) =>
//            {
//                if (healthRange.Value == 75)
//                {
//                    Commands.RemoveComponent(entityInQueryIndex, entity, typeof(RenderMesh));
//                    Commands.AddSharedComponent(entityInQueryIndex, entity, GameController.instance.Zombie_75_RenderMesh);
//                }
//                if (healthRange.Value == 50)
//                {
//                    Commands.RemoveComponent(entityInQueryIndex, entity, typeof(RenderMesh));
//                    Commands.AddSharedComponent(entityInQueryIndex, entity, GameController.instance.Zombie_50_RenderMesh);
//                }
//                if (healthRange.Value == 25)
//                {
//                    Commands.RemoveComponent(entityInQueryIndex, entity, typeof(RenderMesh));
//                    Commands.AddSharedComponent(entityInQueryIndex, entity, GameController.instance.Zombie_25_RenderMesh);
//                }
//            })
//            .Schedule(updateHumanRenderMeshJobHandle);
//        m_EntityCommandBufferSystem.AddJobHandleForProducer(updateZombieRenderMeshJobHandle);

//        return updateZombieRenderMeshJobHandle;
//    }
//}
