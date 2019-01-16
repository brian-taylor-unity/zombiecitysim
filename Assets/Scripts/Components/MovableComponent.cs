using System;
using Unity.Entities;

[Serializable]
public struct Movable : IComponentData { }

public class MovableComponent : ComponentDataWrapper<Movable> { }
