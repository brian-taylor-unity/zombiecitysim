using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Transforms;
using Unity.Mathematics;

[UpdateInGroup(typeof(EndGroup))]
public class AnimateMoveSystem : JobComponentSystem
{
    private float m_TotalTime;

    protected override JobHandle OnUpdate(JobHandle inputDeps)
    {
        m_TotalTime += Time.DeltaTime;

        var percentAnimate = m_TotalTime / GameController.instance.turnDelayTime;
        if (percentAnimate >= 1.0f)
        {
            percentAnimate = 1.0f;
            m_TotalTime = 0.0f;
        }

        var animateMoveJobHandle = Entities
            .WithName("AnimateMove")
            .WithBurst()
            .ForEach((ref Translation translation, ref GridPosition gridPosition, in NextGridPosition nextGridPosition) =>
                {
                    var nextTranslation = math.lerp(new float3(gridPosition.Value), new float3(nextGridPosition.Value), percentAnimate);
                    translation = new Translation { Value = nextTranslation };
                    var clamp = percentAnimate == 1.0f;
                    gridPosition.Value = math.select(gridPosition.Value, nextGridPosition.Value, clamp);
                })
            .Schedule(inputDeps);

        return animateMoveJobHandle;
    }

    protected override void OnCreate()
    {
        m_TotalTime = 0.0f;
    }
}
