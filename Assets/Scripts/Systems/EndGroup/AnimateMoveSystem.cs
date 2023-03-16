using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
public partial struct AnimateMoveJob : IJobEntity
{
    public float percentAnimate;

    public void Execute(ref LocalTransform transform, ref GridPosition gridPosition, in NextGridPosition nextGridPosition)
    {
        var nextTranslation = math.lerp(new float3(gridPosition.Value), new float3(nextGridPosition.Value), percentAnimate);
        transform.Position = nextTranslation;
        var clamp = percentAnimate == 1.0f;
        gridPosition.Value = math.select(gridPosition.Value, nextGridPosition.Value, clamp);
    }
}

[UpdateInGroup(typeof(EndGroup))]
public partial struct AnimateMoveSystem : ISystem
{
    private float m_TotalTime;

    [BurstCompile]
    void OnCreate(ref SystemState state)
    {
        m_TotalTime = 0.0f;

        state.RequireForUpdate<GameControllerComponent>();
    }

    [BurstCompile]
    void OnUpdate(ref SystemState state)
    {
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        m_TotalTime += SystemAPI.Time.DeltaTime;

        var percentAnimate = m_TotalTime / gameControllerComponent.turnDelayTime;
        if (percentAnimate >= 1.0f)
        {
            percentAnimate = 1.0f;
            m_TotalTime = 0.0f;
        }

        new AnimateMoveJob { percentAnimate = percentAnimate }.ScheduleParallel();
    }
}
