using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Collections;

public class RemoveAndSpawnBarrier : BarrierSystem
{
}

[UpdateAfter(typeof(DamageSystem))]
public class RemoveAndSpawnSystem : JobComponentSystem
{
    private ComponentGroup m_HealthGroup;

    [Inject] private RemoveAndSpawnBarrier m_RemoveAndSpawnBarrier;

    [BurstCompile]
    struct RemoveDeadJob : IJobProcessComponentDataWithEntity<Health>
    {
        public EntityCommandBuffer.Concurrent Commands;

        public void Execute(Entity entity, int index, [ReadOnly] ref Health health)
        {
            if (health.Value <= 0)
                Commands.DestroyEntity(index, entity);

            // remove component data for human, change to zombie, etc.
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var componentHealthArray = m_HealthGroup.GetComponentDataArray<Health>();

        var removeDeadJob = new RemoveDeadJob
        {
            Commands = m_RemoveAndSpawnBarrier.CreateCommandBuffer().ToConcurrent(),
        };
        var removeDeadJobHandle = removeDeadJob.Schedule(this, inputDeps);

        return removeDeadJobHandle;
    }

    protected override void OnCreateManager()
    {
        m_HealthGroup = GetComponentGroup(
            ComponentType.ReadOnly(typeof(Health))
        );
    }
}
