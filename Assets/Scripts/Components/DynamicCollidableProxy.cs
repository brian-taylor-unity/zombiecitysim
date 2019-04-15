using System;
using Unity.Entities;

[Serializable]
public struct DynamicCollidable : IComponentData { }

public class DynamicCollidableProxy : ComponentDataProxy<DynamicCollidable> { }
