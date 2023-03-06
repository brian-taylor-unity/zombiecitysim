using Unity.Entities;

[UpdateBefore(typeof(EndGroup))]
public partial class DamageGroup : ComponentSystemGroup
{
}
