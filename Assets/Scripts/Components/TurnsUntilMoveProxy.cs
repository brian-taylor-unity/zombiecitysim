using System;
using Unity.Entities;

[Serializable]
public struct TurnsUntilMove : IComponentData
{
    public int Value;
}

public class TurnsUntilMoveProxy : ComponentDataProxy<TurnsUntilMove> { }
