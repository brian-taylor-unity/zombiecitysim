using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(EndGroup))]
public partial class AdvanceTurnSystem : SystemBase
{
    private EndSimulationEntityCommandBufferSystem m_EntityCommandBufferSystem;
    private double m_LastTime;

    protected override void OnCreate()
    {
        m_LastTime = SystemAPI.Time.ElapsedTime;
        m_EntityCommandBufferSystem = World.GetOrCreateSystemManaged<EndSimulationEntityCommandBufferSystem>();
    }

    protected override void OnUpdate()
    {
        var turnDelayTime = GameController.Instance.turnDelayTime;
        var now = SystemAPI.Time.ElapsedTime;
        if (now - m_LastTime > turnDelayTime)
        {
            m_LastTime = now;

            var humanTurnDelay = GameController.Instance.humanTurnDelay;
            var zombieTurnDelay = GameController.Instance.zombieTurnDelay;

            var advanceHumanTurnJobHandle = Entities
                .WithName("AdvanceHumanTurn")
                .WithAll<Human>()
                .WithBurst()
                .ForEach((ref TurnsUntilActive turnsUntilActive, ref CharacterColor characterColor) =>
                {
                    characterColor.Value.w = math.select(1.0f, math.select(0.85f, 1.0f, turnsUntilActive.Value == 2), turnDelayTime >= 0.2);
                    turnsUntilActive.Value = math.select(turnsUntilActive.Value - 1, humanTurnDelay, turnsUntilActive.Value == 1);
                })
                .ScheduleParallel(Dependency);

            var advanceZombieTurnJobHandle = Entities
                .WithName("AdvanceZombieTurn")
                .WithAll<Zombie>()
                .WithBurst()
                .ForEach((ref TurnsUntilActive turnsUntilActive, ref CharacterColor characterColor) =>
                {
                    characterColor.Value.w = math.select(1.0f, math.select(0.85f, 1.0f, turnsUntilActive.Value == 2), turnDelayTime >= 0.2);
                    turnsUntilActive.Value = math.select(turnsUntilActive.Value - 1, zombieTurnDelay, turnsUntilActive.Value == 1);
                })
                .ScheduleParallel(advanceHumanTurnJobHandle);

            var audibleDecayTime = GameController.Instance.audibleDecayTime;
            var commands = m_EntityCommandBufferSystem.CreateCommandBuffer().AsParallelWriter();
            var advanceAudibleAgeJobHandle = Entities
                .WithName("AdvanceAudibleAge")
                .WithBurst()
                .ForEach((Entity entity, int entityInQueryIndex, ref Audible audible) =>
                    {
                        audible.Age += 1;
                        if (audible.Age > audibleDecayTime)
                            commands.DestroyEntity(entityInQueryIndex, entity);
                    })
                .ScheduleParallel(Dependency);
            m_EntityCommandBufferSystem.AddJobHandleForProducer(advanceAudibleAgeJobHandle);

            Dependency = JobHandle.CombineDependencies(advanceZombieTurnJobHandle, advanceAudibleAgeJobHandle);
        }
    }
}
