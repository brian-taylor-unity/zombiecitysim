using Unity.Entities;

[UpdateBefore(typeof(MoveUnitsGroup))]
public partial class InitialGroup : ComponentSystemGroup
{
}
