using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;

[BurstCompile]
public partial struct CalculateDamageJob : IJobEntity
{
    [ReadOnly] public NativeParallelHashMap<int, int> DamageTakingHashMap;
    public NativeParallelMultiHashMap<int, int>.ParallelWriter DamageAmountHashMapParallelWriter;

    public void Execute(in GridPosition gridPosition, in Damage damage, in TurnsUntilActive turnsUntilActive)
    {
        if (turnsUntilActive.Value != 1)
            return;

        for (var z = -1; z <= 1; z++)
        {
            for (var x = -1; x <= 1; x++)
            {
                if (x == 0 && z == 0)
                    continue;

                var damageKey = (int)math.hash(new int3(gridPosition.Value.x + x, gridPosition.Value.y, gridPosition.Value.z + z));
                if (DamageTakingHashMap.TryGetValue(damageKey, out _))
                    DamageAmountHashMapParallelWriter.Add(damageKey, damage.Value);
            }
        }
    }
}