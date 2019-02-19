using System;
using Unity.Entities;

[Serializable]
public struct StaticCollidable : IComponentData { }

public class StaticCollidableComponent : ComponentDataProxy<StaticCollidable> { }
