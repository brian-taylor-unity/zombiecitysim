using Unity.Mathematics;

public struct GridHash
{
    public static int Hash(int3 gridPosition)
    {
        unchecked
        {
            int hash = gridPosition.x;
            hash = (hash * 397) ^ gridPosition.y;
            hash = (hash * 397) ^ gridPosition.z;
            hash += hash << 3;
            hash ^= hash >> 11;
            hash += hash << 15;
            return hash;
        }
    }
}
