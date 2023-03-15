using System;
using Unity.Entities;

[Serializable]
public struct Vision : IComponentData
{
    public int Distance;
}
