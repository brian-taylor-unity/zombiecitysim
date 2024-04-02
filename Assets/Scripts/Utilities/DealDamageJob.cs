using System;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[BurstCompile]
public partial struct DealDamageJob : IJobEntity
{
    public float4 FullHealthColor;
    [ReadOnly] public NativeParallelMultiHashMap<uint, int> DamageAmountHashMap;

    public void Execute(ref Health health, ref URPMaterialPropertyBaseColor materialColor, [ReadOnly] in MaxHealth maxHealth, [ReadOnly] in GridPosition gridPosition)
    {
        var myHealth = health.Value;

        var gridPositionHash = math.hash(new int3(gridPosition.Value));
        if (!DamageAmountHashMap.TryGetFirstValue(gridPositionHash, out var damage, out var it))
            return;

        myHealth -= damage;
        while (DamageAmountHashMap.TryGetNextValue(out damage, ref it))
        {
            myHealth -= damage;
        }

        var lerp = math.lerp(0.0f, 1.0f, (float)myHealth / maxHealth.Value);
        materialColor.Value = new float4(
            Math.Abs(FullHealthColor.x - 1.0f) < 0.001f ? lerp : 1.0f - lerp,
            Math.Abs(FullHealthColor.y - 1.0f) < 0.001f ? lerp : 1.0f - lerp,
            0.0f,
            materialColor.Value.w
        );
        health.Value = myHealth;
    }
}