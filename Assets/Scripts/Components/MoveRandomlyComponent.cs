using System;
using Unity.Entities;

[Serializable]
public struct MoveRandomly : IComponentData { }

public class MoveRandomlyComponent : ComponentDataProxy<MoveRandomly> { }
