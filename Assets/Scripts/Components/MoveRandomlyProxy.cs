using System;
using Unity.Entities;

[Serializable]
public struct MoveRandomly : IComponentData { }

public class MoveRandomlyProxy : ComponentDataProxy<MoveRandomly> { }
