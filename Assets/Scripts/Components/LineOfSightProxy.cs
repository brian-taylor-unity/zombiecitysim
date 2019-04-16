using System;
using Unity.Entities;

[Serializable]
public struct LineOfSight : IComponentData { }

public class LineOfSightProxy : ComponentDataProxy<LineOfSight> { }
