using Unity.Collections;
using Unity.Mathematics;

public static class LineOfSightUtilities
{
    public static bool InLineOfSight(int3 initialGridPosition, int3 targetGridPosition, NativeParallelHashMap<int, int> staticCollidableHashMap)
    {
        float vx, vz, ox, oz, l;
        int i;
        vx = targetGridPosition.x - initialGridPosition.x;
        vz = targetGridPosition.z - initialGridPosition.z;
        ox = targetGridPosition.x + 0.5f;
        oz = targetGridPosition.z + 0.5f;
        l = math.sqrt((vx * vx) + (vz * vz));
        vx /= l;
        vz /= l;
        for (i = 0; i < (int)l; i++)
        {
            int3 gridPosition = new int3((int)math.floor(ox), initialGridPosition.y, (int)math.floor(oz));
            int key = (int)math.hash(gridPosition);
            if (staticCollidableHashMap.TryGetValue(key, out _))
                return false;

            ox += vx;
            oz += vz;
        }

        return true;
    }
}
