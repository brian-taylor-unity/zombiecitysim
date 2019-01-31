using System;
using Unity.Entities;

[Serializable]
public struct Audible : IComponentData { }

public class AudibleComponent : ComponentDataWrapper<Audible> { }
