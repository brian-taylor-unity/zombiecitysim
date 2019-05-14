using System;
using Unity.Entities;

[Serializable]
public struct MoveTowardsTarget : IComponentData
{
}

public class MoveTowardsTargetProxy : ComponentDataProxy<MoveTowardsTarget> { }
