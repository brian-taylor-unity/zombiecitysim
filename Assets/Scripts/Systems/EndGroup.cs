using Unity.Entities;

[UpdateAfter(typeof(DamageGroup))]
public partial class EndGroup : ComponentSystemGroup
{
}
