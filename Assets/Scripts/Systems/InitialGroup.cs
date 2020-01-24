using Unity.Entities;

[UpdateBefore(typeof(MoveUnitsGroup))]
public class InitialGroup : ComponentSystemGroup
{
}
