using System;
using Unity.Entities;

[Serializable]
public struct MoveEscapeTarget : IComponentData { }

public class MoveEscapeTargetComponent : ComponentDataProxy<MoveEscapeTarget> { }
