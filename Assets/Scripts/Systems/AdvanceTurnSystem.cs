using Unity.Burst;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;

[UpdateAfter(typeof(RemoveDeadUnitsSystem))]
public class AdvanceTurnSystem : JobComponentSystem
{
    private EntityQuery m_Humans;
    private EntityQuery m_Zombies;

    [BurstCompile]
    struct AdvanceTurnJob : IJobForEach<TurnsUntilMove>
    {
        public int turnDelay;

        public void Execute(ref TurnsUntilMove turnsUntilMove)
        {
            var reset = turnsUntilMove.Value == 0;
            turnsUntilMove.Value = math.select(turnsUntilMove.Value - 1, turnDelay, reset);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        var advanceHumanTurnJob = new AdvanceTurnJob
        {
            turnDelay = Bootstrap.HumanTurnDelay,
        };
        var advanceHumanTurnJobHandle = advanceHumanTurnJob.Schedule(m_Humans, inputDeps);

        var advanceZombieTurnJob = new AdvanceTurnJob
        {
            turnDelay = Bootstrap.ZombieTurnDelay,
        };
        var advanceZombieTurnJobHandle = advanceZombieTurnJob.Schedule(m_Zombies, advanceHumanTurnJobHandle);

        return advanceZombieTurnJobHandle;
    }

    protected override void OnCreateManager()
    {
        m_Humans = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            typeof(TurnsUntilMove)
        );
        m_Zombies = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Zombie)),
            typeof(TurnsUntilMove)
        );
    }
}
