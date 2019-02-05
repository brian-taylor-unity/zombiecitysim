using System;
using Unity.Entities;
using Unity.Mathematics;

[Serializable]
public struct Audible : IComponentData
{
    public int3 Value;
}

public class AudibleComponent : ComponentDataWrapper<Audible> { }
