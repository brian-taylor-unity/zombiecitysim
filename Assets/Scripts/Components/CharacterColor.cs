using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[MaterialProperty("_CharacterColor")]
public struct CharacterColor : IComponentData
{
    public float4 Value;
}
