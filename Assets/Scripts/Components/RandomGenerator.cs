using System;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

[Serializable]
public struct RandomGenerator : IComponentData
{
    public Random Value;
}
