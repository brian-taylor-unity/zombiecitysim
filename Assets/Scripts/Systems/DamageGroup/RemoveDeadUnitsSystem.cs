using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

[UpdateInGroup(typeof(DamageGroup))]
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

    [BurstCompile]
    struct RemoveAudibleJob : IJobForEachWithEntity<Audible>
    {
        public EntityCommandBuffer.Concurrent Commands;

        public void Execute(Entity entity, int index, ref Audible audible)
        {
            audible.Age++;

            if (audible.Age > 5)
                Commands.DestroyEntity(index, entity);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var Commands = m_EntityCommandBufferSystem.CreateCommandBuffer().ToConcurrent();

        var removeDeadJob = new RemoveDeadJob
        {
            Commands = Commands,
        };
        var removeDeadJobHandle = removeDeadJob.ScheduleSingle(this, inputDeps);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(removeDeadJobHandle);

        var removeAudibleJob = new RemoveAudibleJob
        {
            Commands = Commands,
        };
        var removeAudibleJobHandle = removeAudibleJob.ScheduleSingle(this, removeDeadJobHandle);
        m_EntityCommandBufferSystem.AddJobHandleForProducer(removeAudibleJobHandle);

        return removeAudibleJobHandle;
    }

    protected override void OnCreate()
    {
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }
}
