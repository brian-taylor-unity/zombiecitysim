using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

[UpdateAfter(typeof(SpawnZombiesFromDeadHumansSystem))]
public class RemoveDeadUnitsSystem : JobComponentSystem
{
    EntityCommandBufferSystem m_EntityCommandBufferSystem;

    [BurstCompile]
    struct RemoveDeadJob : IJobForEachWithEntity<Health>
    {
        public EntityCommandBuffer.Concurrent Commands;

        public void Execute(Entity entity, int index, [ReadOnly] ref Health health)
        {
            if (health.Value <= 0)
                Commands.DestroyEntity(index, entity);

        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var removeDeadJob = new RemoveDeadJob
        {
            Commands = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent(),
        };
        var removeDeadJobHandle = removeDeadJob.ScheduleSingle(this, inputDeps);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(removeDeadJobHandle);

        return removeDeadJobHandle;
    }

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EntityCommandBufferSystem>();
    }
}
