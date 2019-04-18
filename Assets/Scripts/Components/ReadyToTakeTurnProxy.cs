using System;
using Unity.Entities;

[Serializable]
public struct ReadyToTakeTurn : IComponentData { }

public class ReadyToTakeTurnProxy : ComponentDataProxy<ReadyToTakeTurn> { }
