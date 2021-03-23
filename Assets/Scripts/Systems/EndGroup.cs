using Unity.Entities;

[UpdateAfter(typeof(DamageGroup))]
public class EndGroup : ComponentSystemGroup
{
}
