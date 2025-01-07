using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct CalculateDamageJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<uint, int> DamageTakingHashMap;
    public NativeParallelMultiHashMap<uint, int>.ParallelWriter DamageAmountHashMapParallelWriter;

    public void Execute([ReadOnly] in GridPosition gridPosition, [ReadOnly] in Damage damage)
    {
        if (damage.Value == 0)
            return;

        for (var z = -1; z <= 1; z++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && z == 0)
                    continue;

                var damageKey = math.hash(new int3(gridPosition.Value.x + x, gridPosition.Value.y, gridPosition.Value.z + z));
                if (DamageTakingHashMap.TryGetValue(damageKey, out _))
                    DamageAmountHashMapParallelWriter.Add(damageKey, damage.Value);
            }
        }
    }
}