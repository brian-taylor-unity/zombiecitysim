using Unity.Entities;

[UpdateBefore(typeof(DamageGroup))]
public partial class MoveUnitsGroup : ComponentSystemGroup
{
}
