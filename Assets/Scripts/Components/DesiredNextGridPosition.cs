using Unity.Entities;
using Unity.Mathematics;

public struct DesiredNextGridPosition : IComponentData
{
    public int3 Value;
}
