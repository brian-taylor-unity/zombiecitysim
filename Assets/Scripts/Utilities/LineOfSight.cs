using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

[BurstCompile]
public static class LineOfSightUtilities
{
    [BurstCompile]
    public static bool InLineOfSight([ReadOnly] in int3 initialGridPosition, [ReadOnly] in int3 targetGridPosition, [ReadOnly] in NativeParallelHashMap<uint, int> staticCollidableHashMap)
    {
        float vx = targetGridPosition.x - initialGridPosition.x;
        float vz = targetGridPosition.z - initialGridPosition.z;
        var ox = targetGridPosition.x + 0.5f;
        var oz = targetGridPosition.z + 0.5f;
        var l = math.sqrt((vx * vx) + (vz * vz));
        vx /= l;
        vz /= l;
        for (var i = 0; i < (int)l; i++)
        {
            var gridPosition = new int3((int)math.floor(ox), initialGridPosition.y, (int)math.floor(oz));
            var key = math.hash(gridPosition);
            if (staticCollidableHashMap.TryGetValue(key, out _))
                return false;

            ox += vx;
            oz += vz;
        }

        return true;
    }

    [BurstCompile]
    public static bool InLineOfSightUpdated([ReadOnly] in int3 initialGridPosition, [ReadOnly] in int3 targetGridPosition, [ReadOnly] in NativeParallelHashMap<uint, int> staticCollidableHashMap)
    {
        var dx = targetGridPosition.x - initialGridPosition.x;
        var dz = targetGridPosition.z - initialGridPosition.z;

        var x = initialGridPosition.x;
        var z = initialGridPosition.z;
        var error = 0;
        var errorIncrement1 = dz * 2;
        var errorIncrement2 = (dz - dx) * 2;
        var sz = (int)math.sign(dz);

        for (var i = 0; i <= dx; i++, x++)
        {
            if (staticCollidableHashMap.TryGetValue(math.hash(new int3(x, initialGridPosition.y, z)), out _))
                return false;

            error += errorIncrement1;
            if (error <= dx)
                continue;

            error -= errorIncrement2;
            z += sz;
        }

        return true;
    }
}
