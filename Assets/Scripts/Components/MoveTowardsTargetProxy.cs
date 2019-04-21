using System;
using Unity.Entities;

[Serializable]
public struct MoveTowardsTarget : IComponentData
{
    public int TurnsSinceMove;
}

public class MoveTowardsTargetProxy : ComponentDataProxy<MoveTowardsTarget> { }
