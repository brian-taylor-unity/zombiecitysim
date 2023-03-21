using System;
using Unity.Burst;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
public partial struct AnimateMoveJob : IJobEntity
{
    public float PercentAnimate;

    public void Execute(ref LocalTransform transform, ref GridPosition gridPosition, in NextGridPosition nextGridPosition)
    {
        var nextTranslation = math.lerp(new float3(gridPosition.Value), new float3(nextGridPosition.Value), PercentAnimate);
        transform.Position = nextTranslation;
        gridPosition.Value = math.select(gridPosition.Value, nextGridPosition.Value, Math.Abs(PercentAnimate - 1.0f) < 0.0001);
    }
}

[UpdateInGroup(typeof(EndGroup))]
public partial struct AnimateMoveSystem : ISystem
{
    private float _totalTime;

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _totalTime = 0.0f;

        state.RequireForUpdate<GameControllerComponent>();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var gameControllerComponent = SystemAPI.GetSingleton<GameControllerComponent>();

        _totalTime += SystemAPI.Time.DeltaTime;

        var percentAnimate = _totalTime / gameControllerComponent.turnDelayTime;
        if (percentAnimate >= 1.0f)
        {
            percentAnimate = 1.0f;
            _totalTime = 0.0f;
        }

        new AnimateMoveJob { PercentAnimate = percentAnimate }.ScheduleParallel();
    }
}
