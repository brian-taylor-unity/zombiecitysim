using Unity.Entities;
using Random = Unity.Mathematics.Random;

public struct RandomGenerator : IComponentData
{
    public Random Value;
}
