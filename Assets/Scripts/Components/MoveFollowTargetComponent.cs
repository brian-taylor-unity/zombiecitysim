using System;
using Unity.Entities;

[Serializable]
public struct MoveFollowTarget : IComponentData { }

public class MoveFollowTargetComponent : ComponentDataWrapper<MoveFollowTarget> { }
