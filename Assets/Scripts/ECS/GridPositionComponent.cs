using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct GridPosition : IComponentData
{
    public int3 Value;
}

public class GridPositionComponent : ComponentDataWrapper<GridPosition> { }
