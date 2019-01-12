using System;
using Unity.Entities;

[Serializable]
public struct Collidable : IComponentData { }

public class CollidableComponent : ComponentDataWrapper<Collidable> { }
