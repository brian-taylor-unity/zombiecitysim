using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct DesiredNextGridPosition : IComponentData
{
    public int3 Value;
}
