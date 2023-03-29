using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;

public static class LineOfSightUtilities
{
    [BurstCompile]
    public static bool InLineOfSight(int3 initialGridPosition, int3 targetGridPosition, NativeParallelHashMap<int, int> staticCollidableHashMap)
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
            var key = (int)math.hash(gridPosition);
            if (staticCollidableHashMap.TryGetValue(key, out _))
                return false;

            ox += vx;
            oz += vz;
        }

        return true;
    }
}
