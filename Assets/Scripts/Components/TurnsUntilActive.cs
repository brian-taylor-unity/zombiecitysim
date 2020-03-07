using System;
using Unity.Entities;

[Serializable]
public struct TurnsUntilActive : IComponentData
{
    public int Value;
}
