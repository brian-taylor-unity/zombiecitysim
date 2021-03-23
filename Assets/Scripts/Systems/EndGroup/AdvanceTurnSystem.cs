using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(EndGroup))]
public class AdvanceTurnSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystem;
    private double m_LastTime;

    protected override void OnCreate()
    {
        m_LastTime = Time.ElapsedTime;
        m_EntityCommandBufferSystem = World.GetOrCreateSystem<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var humanTurnDelay = GameController.instance.humanTurnDelay;
        var zombieTurnDelay = GameController.instance.zombieTurnDelay;

        var resetHumanTurnJobHandle = Entities
            .WithName("ResetHumanTurn")
            .WithAll<Human>()
            .WithBurst()
            .ForEach((ref TurnsUntilActive turnsUntilActive) =>
                {
                    turnsUntilActive.Value = math.select(turnsUntilActive.Value, humanTurnDelay, turnsUntilActive.Value == 0);
                })
            .ScheduleParallel(Dependency);

        var resetZombieTurnJobHandle = Entities
            .WithName("ResetZombieTurn")
            .WithAll<Zombie>()
            .WithBurst()
            .ForEach((ref TurnsUntilActive turnsUntilActive) =>
            {
                turnsUntilActive.Value = math.select(turnsUntilActive.Value, zombieTurnDelay, turnsUntilActive.Value == 0);
            })
            .ScheduleParallel(resetHumanTurnJobHandle);

        var outputDeps = resetZombieTurnJobHandle;

        var now = Time.ElapsedTime;
        if (now - m_LastTime > GameController.instance.turnDelayTime)
        {
            m_LastTime = now;

            var advanceTurnsUntilActiveJobHandle = Entities
                .WithName("AdvanceTurn")
                .WithAny<Human, Zombie>()
                .WithBurst()
                .ForEach((ref TurnsUntilActive turnsUntilActive) =>
                    {
                        turnsUntilActive.Value -= 1;
                    })
                .ScheduleParallel(outputDeps);

            var commands = m_EntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            var advanceAudibleAgeJobHandle = Entities
                .WithName("AdvanceAudibleAge")
                .WithBurst()
                .ForEach((Entity entity, int entityInQueryIndex, ref Audible audible) =>
                    {
                        audible.Age += 1;
                        if (audible.Age > 5)
                            commands.DestroyEntity(entityInQueryIndex, entity);
                    })
                .ScheduleParallel(outputDeps);
            m_EntityCommandBufferSystem.AddJobHandleForProducer(advanceAudibleAgeJobHandle);

            outputDeps = JobHandle.CombineDependencies(advanceTurnsUntilActiveJobHandle, advanceAudibleAgeJobHandle);
        }

        Dependency = outputDeps;
    }
}
