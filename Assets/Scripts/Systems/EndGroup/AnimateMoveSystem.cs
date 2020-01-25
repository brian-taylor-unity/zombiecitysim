using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

[UpdateInGroup(typeof(EndGroup))]
public class AnimateMoveSystem : JobComponentSystem
{
    private EntityQuery m_MovingUnits;
    private float m_TotalTime;

    [BurstCompile]
    struct AnimateMoveJob : IJobForEach<GridPosition, NextGridPosition, Translation>
    {
        public float percentAnimate;

        public void Execute(ref GridPosition gridPosition, [ReadOnly] ref NextGridPosition nextGridPosition, ref Translation translation)
        {
            var nextTranslation = math.lerp(new float3(gridPosition.Value), new float3(nextGridPosition.Value), percentAnimate);
            translation = new Translation { Value = nextTranslation };
            var clamp = percentAnimate == 1.0f;
            gridPosition.Value = math.select(gridPosition.Value, nextGridPosition.Value, clamp);
        }
    }

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_TotalTime += Time.DeltaTime;

        var percentAnimate = m_TotalTime / GameController.instance.turnDelayTime;
        if (percentAnimate >= 1.0f)
        {
            percentAnimate = 1.0f;
            m_TotalTime = 0.0f;
        }

        var animateMoveJob = new AnimateMoveJob
        {
            percentAnimate = percentAnimate,
        };
        var animateMoveJobHandle = animateMoveJob.Schedule(m_MovingUnits, inputDeps);

        return animateMoveJobHandle;
    }

    protected override void OnCreate()
    {
        m_MovingUnits = GetEntityQuery(
            typeof(GridPosition),
            ComponentType.ReadOnly(typeof(NextGridPosition)),
            typeof(Translation)
        );

        m_TotalTime = 0.0f;
    }
}
