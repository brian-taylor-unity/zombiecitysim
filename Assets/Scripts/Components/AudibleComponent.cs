using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct Audible : IComponentData
{
    public int3 GridPositionValue;
    public int3 Target;
    public int Age;
}

public class AudibleComponent : ComponentDataProxy<Audible> { }
