using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

public class RemoveDeadUnitsBarrier : BarrierSystem
{
}

[UpdateAfter(typeof(SpawnZombiesFromDeadHumansSystem))]
public class RemoveDeadUnitsSystem : JobComponentSystem
{
    [Inject] private RemoveDeadUnitsBarrier m_RemoveAndSpawnBarrier;

    [BurstCompile]
    struct RemoveDeadJob : IJobProcessComponentDataWithEntity<Health>
    {
        public EntityCommandBuffer.Concurrent Commands;

        public void Execute(Entity entity, int index, [ReadOnly] ref Health health)
        {
            if (health.Value <= 0)
                Commands.DestroyEntity(index, entity);

        }
    }

    [BurstCompile]
    struct RemoveAudibleJob : IJobProcessComponentDataWithEntity<Audible>
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
        var Commands = m_RemoveAndSpawnBarrier.CreateCommandBuffer().ToConcurrent();

        var removeDeadJob = new RemoveDeadJob
        {
            Commands = Commands,
        };
        var removeDeadJobHandle = removeDeadJob.Schedule(this, inputDeps);

        var removeAudibleJob = new RemoveAudibleJob
        {
            Commands = Commands,
        };
        var removeAudibleJobHandle = removeAudibleJob.Schedule(this, removeDeadJobHandle);

        return removeAudibleJobHandle;
    }
}
