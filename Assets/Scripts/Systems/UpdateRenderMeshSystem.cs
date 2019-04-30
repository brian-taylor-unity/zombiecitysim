using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Rendering;

[UpdateAfter(typeof(DamageSystem))]
public class UpdateRenderMeshSystem : JobComponentSystem
{
    EntityCommandBufferSystem m_EntityCommandBufferSystem;

    private EntityQuery m_Humans;

    struct SetRenderMeshJob : IJobForEachWithEntity<Health>
    {
        public EntityCommandBuffer.Concurrent Commands;
        public int health_75;
        [ReadOnly] public RenderMesh renderMesh_75;
        public int health_50;
        [ReadOnly] public RenderMesh renderMesh_50;
        public int health_25;
        [ReadOnly] public RenderMesh renderMesh_25;

        public void Execute(Entity entity, int index, ref Health health)
        {
            if (health.Value < health_75)
            { 
                Commands.RemoveComponent(index, entity, typeof(RenderMesh));
                Commands.AddSharedComponent(index, entity, renderMesh_75);
            }
            if (health.Value < health_50)
            {
                Commands.RemoveComponent(index, entity, typeof(RenderMesh));
                Commands.AddSharedComponent(index, entity, renderMesh_50);
            }
            if (health.Value < health_25)
            {
                Commands.RemoveComponent(index, entity, typeof(RenderMesh));
                Commands.AddSharedComponent(index, entity, renderMesh_25);
            }
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var Commands = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var setRenderMeshJob = new SetRenderMeshJob
        {
            Commands = Commands,
            health_75 = (int)(GameController.instance.humanStartingHealth * 0.75),
            renderMesh_75 = Bootstrap.HumanMeshInstanceRenderer_Health_75,
            health_50 = (int)(GameController.instance.humanStartingHealth * 0.5),
            renderMesh_50 = Bootstrap.HumanMeshInstanceRenderer_Health_50,
            health_25 = (int)(GameController.instance.humanStartingHealth * 0.25),
            renderMesh_25 = Bootstrap.HumanMeshInstanceRenderer_Health_25,
        };
        var setRenderMeshJobHandle = setRenderMeshJob.Schedule(this, inputDeps);

        return setRenderMeshJobHandle;
    }

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EntityCommandBufferSystem>();
        
        m_Humans = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Health)),
            typeof(RenderMesh)
        );
        m_Humans.SetFilterChanged(typeof(Health));
    }
}
