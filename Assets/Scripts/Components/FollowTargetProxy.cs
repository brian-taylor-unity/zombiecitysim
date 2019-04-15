using System;
using Unity.Entities;

[Serializable]
public struct FollowTarget : IComponentData { }

public class FollowTargetProxy : ComponentDataProxy<FollowTarget> { }
