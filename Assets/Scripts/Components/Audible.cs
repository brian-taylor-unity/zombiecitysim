using Unity.Entities;
using Unity.Mathematics;

public struct Audible : IComponentData
{
    public int3 GridPositionValue;
    public int3 Target;
    public int Age;
}
