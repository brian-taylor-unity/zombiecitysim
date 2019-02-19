using System;
using Unity.Entities;

[Serializable]
public struct Human : IComponentData { }

public class HumanComponent : ComponentDataProxy<Human> { }
