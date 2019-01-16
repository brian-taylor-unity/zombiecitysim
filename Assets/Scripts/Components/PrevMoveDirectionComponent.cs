using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct PrevMoveDirection : IComponentData
{
    public int3 Value;
}

public class PrevMoveDirectionComponent : ComponentDataWrapper<PrevMoveDirection> { }
