using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct NextGridPosition : IComponentData
{
    public int3 Value;
}

public class NextGridPositionProxy : ComponentDataProxy<NextGridPosition> { }
