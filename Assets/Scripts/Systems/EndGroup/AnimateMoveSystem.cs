using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;

[BurstCompile]
public partial struct AnimateMoveJob : IJobEntity
{
    public float PercentAnimate;

    public void Execute(ref LocalTransform transform, ref GridPosition gridPosition, [ReadOnly] in DesiredNextGridPosition desiredNextGridPosition)
    {
        var nextTranslation = math.lerp(new float3(gridPosition.Value), new float3(desiredNextGridPosition.Value), PercentAnimate);
        transform.Position = nextTranslation;
        gridPosition.Value = math.select(gridPosition.Value, desiredNextGridPosition.Value, Math.Abs(PercentAnimate - 1.0f) < 0.0001);
    }
}

[UpdateInGroup(typeof(EndGroup))]
[RequireMatchingQueriesForUpdate]
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
