using System;
using Unity.Entities;

[Serializable]
public struct LineOfSight : IComponentData { }

public class LineOfSightComponent : ComponentDataWrapper<LineOfSight> { }
