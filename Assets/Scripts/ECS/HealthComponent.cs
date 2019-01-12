using System;
using Unity.Entities;

[Serializable]
public struct Health : IComponentData
{
    public int Value;
}

public class HealthComponent : ComponentDataWrapper<Health> { }
