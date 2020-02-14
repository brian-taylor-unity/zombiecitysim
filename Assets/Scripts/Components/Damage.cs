using System;
using Unity.Entities;

[Serializable]
public struct Damage : IComponentData
{
    public int Value;
}
