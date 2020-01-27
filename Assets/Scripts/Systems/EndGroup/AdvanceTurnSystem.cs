using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateInGroup(typeof(EndGroup))]
public class AdvanceTurnSystem : JobComponentSystem
{
    private EntityQuery m_Humans;
    private EntityQuery m_Zombies;
    private double m_LastTime;

    [BurstCompile]
    struct ResetTurnJob : IJobForEach<TurnsUntilActive>
    {
        public int turnDelay;

        public void Execute(ref TurnsUntilActive TurnsUntilActive)
        {
            var reset = TurnsUntilActive.Value == 0;
            TurnsUntilActive.Value = math.select(TurnsUntilActive.Value, turnDelay, reset);
        }
    }

    [BurstCompile]
    struct AdvanceTurnJob : IJobForEach<TurnsUntilActive>
    {
        public void Execute(ref TurnsUntilActive TurnsUntilActive)
        {
            TurnsUntilActive.Value -= 1;
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var resetHumanTurnJob = new ResetTurnJob
        {
            turnDelay = GameController.instance.humanTurnDelay,
        };
        var resetHumanTurnJobHandle = resetHumanTurnJob.Schedule(m_Humans, inputDeps);

        var resetZombieTurnJob = new ResetTurnJob
        {
            turnDelay = GameController.instance.zombieTurnDelay,
        };
        var resetZombieTurnJobHandle = resetZombieTurnJob.Schedule(m_Zombies, resetHumanTurnJobHandle);

        var outputDeps = resetZombieTurnJobHandle;

        var now = Time.ElapsedTime;
        if (now - m_LastTime > GameController.instance.turnDelayTime)
        {
            m_LastTime = now;

            var advanceHumanTurnJob = new AdvanceTurnJob
            {
            };
            var advanceHumanTurnJobHandle = advanceHumanTurnJob.Schedule(m_Humans, outputDeps);

            var advanceZombieTurnJob = new AdvanceTurnJob
            {
            };
            var advanceZombieTurnJobHandle = advanceZombieTurnJob.Schedule(m_Zombies, advanceHumanTurnJobHandle);

            outputDeps = JobHandle.CombineDependencies(advanceHumanTurnJobHandle, advanceZombieTurnJobHandle);
        }

        return outputDeps;
    }

    protected override void OnCreate()
    {
        m_Humans = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            typeof(TurnsUntilActive)
        );
        m_Zombies = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Zombie)),
            typeof(TurnsUntilActive)
        );

        m_LastTime = Time.ElapsedTime;
    }
}
