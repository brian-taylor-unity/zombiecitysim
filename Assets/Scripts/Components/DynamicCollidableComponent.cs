using System;
using Unity.Entities;

[Serializable]
public struct DynamicCollidable : IComponentData { }

public class DynamicCollidableComponent : ComponentDataProxy<DynamicCollidable> { }
