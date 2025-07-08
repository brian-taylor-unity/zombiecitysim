using Unity.Burst;
using Unity.Collections;
using Unity.Entities;

[BurstCompile]
public partial struct RemoveDeadBuilderAgentsJob : IJobEntity
{
    public EntityCommandBuffer.ParallelWriter Ecb;

    public void Execute([EntityIndexInQuery] int entityIndexInQuery, Entity entity, [ReadOnly] in BuilderLifetime builderLifetime)
    {
        if (builderLifetime.Value <= 0)
        {
            Ecb.DestroyEntity(entityIndexInQuery, entity);
        }
    }
}

[UpdateInGroup(typeof(InitializationSystemGroup))]
public partial struct RemoveDeadBuilderAgentsSystem : ISystem
{
    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        state.RequireForUpdate<BeginSimulationEntityCommandBufferSystem.Singleton>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        state.Dependency = new RemoveDeadBuilderAgentsJob
        {
            Ecb = SystemAPI.GetSingleton<BeginSimulationEntityCommandBufferSystem.Singleton>()
                .CreateCommandBuffer(state.WorldUnmanaged).AsParallelWriter()
        }.ScheduleParallel(state.Dependency);
    }

    [BurstCompile]
    public void OnDestroy(ref SystemState state)
    {

    }
}
