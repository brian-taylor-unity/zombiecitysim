using System;
using Unity.Entities;
using Random = Unity.Mathematics.Random;

[Serializable]
public struct RandomComponent : IComponentData
{
    public Random Value;
}
