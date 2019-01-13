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

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var removeDeadJob = new RemoveDeadJob
        {
            Commands = m_RemoveAndSpawnBarrier.CreateCommandBuffer().ToConcurrent(),
        };
        var removeDeadJobHandle = removeDeadJob.Schedule(this, inputDeps);

        return removeDeadJobHandle;
    }
}
