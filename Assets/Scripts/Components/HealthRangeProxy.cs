using System;
using Unity.Entities;

[Serializable]
public struct HealthRange : IComponentData
{
    public int Value;
}

public class HealthRangeProxy : ComponentDataProxy<HealthRange> { }
