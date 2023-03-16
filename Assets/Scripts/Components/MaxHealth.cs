using System;
using Unity.Entities;

[Serializable]
public partial struct MaxHealth : IComponentData
{
    public int Value;
}