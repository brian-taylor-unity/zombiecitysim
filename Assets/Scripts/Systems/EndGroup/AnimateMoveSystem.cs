using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[UpdateInGroup(typeof(EndGroup))]
public partial class AnimateMoveSystem : SystemBase
{
    private float m_TotalTime;

    protected override void OnUpdate()
    {
        m_TotalTime += Time.DeltaTime;

        var percentAnimate = m_TotalTime / GameController.instance.turnDelayTime;
        if (percentAnimate >= 1.0f)
        {
            percentAnimate = 1.0f;
            m_TotalTime = 0.0f;
        }

        Entities
            .WithName("AnimateMove")
            .WithBurst()
            .ForEach((ref Translation translation, ref GridPosition gridPosition, in NextGridPosition nextGridPosition) =>
                {
                    var nextTranslation = math.lerp(new float3(gridPosition.Value), new float3(nextGridPosition.Value), percentAnimate);
                    translation = new Translation { Value = nextTranslation };
                    var clamp = percentAnimate == 1.0f;
                    gridPosition.Value = math.select(gridPosition.Value, nextGridPosition.Value, clamp);
                })
            .ScheduleParallel();
    }

    protected override void OnCreate()
    {
        m_TotalTime = 0.0f;
    }
}
