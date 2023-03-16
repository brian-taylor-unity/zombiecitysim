using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct DealDamageJob : IJobEntity
{
    [ReadOnly] public NativeParallelMultiHashMap<int, int> DamageAmountHashMap;

    public void Execute(ref Health health, ref CharacterColor materialColor, in MaxHealth maxHealth, in GridPosition gridPosition)
    {
        int myHealth = health.Value;

        int gridPositionHash = (int)math.hash(new int3(gridPosition.Value));
        if (DamageAmountHashMap.TryGetFirstValue(gridPositionHash, out var damage, out var it))
        {
            myHealth -= damage;

            while (DamageAmountHashMap.TryGetNextValue(out damage, ref it))
            {
                myHealth -= damage;
            }

            var lerp = math.lerp(0.0f, 1.0f, (float)myHealth / maxHealth.Value);
            materialColor.Value = new float4(1.0f - lerp, lerp, 0.0f, 1.0f);
            health.Value = myHealth;
        }
    }
}