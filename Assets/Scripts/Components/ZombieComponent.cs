using System;
using Unity.Entities;

[Serializable]
public struct Zombie : IComponentData { }

public class ZombieComponent : ComponentDataProxy<Zombie> { }
