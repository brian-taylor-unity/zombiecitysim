using Unity.Entities;
using Unity.Mathematics;
using Unity.Rendering;

[MaterialProperty("_CharacterColor", MaterialPropertyFormat.Float4)]
public struct CharacterColor : IComponentData
{
    public float4 Value;
}
