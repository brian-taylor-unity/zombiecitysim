using System;
using Unity.Entities;

[Serializable]
public struct MoveEscapeTarget : IComponentData { }

public class MoveEscapeTargetProxy : ComponentDataProxy<MoveEscapeTarget> { }
