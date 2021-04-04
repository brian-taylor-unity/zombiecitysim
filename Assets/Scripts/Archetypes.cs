using Unity.Entities;
using UnityEngine;

public class Archetypes
{
    public static EntityArchetype HumanArchetype;
    public static EntityArchetype ZombieArchetype;

    public static EntityArchetype AudibleArchetype;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        HumanArchetype = World.DefaultGameObjectInjectionWorld.EntityManager.CreateArchetype(
            typeof(Human),
            typeof(GridPosition),
            typeof(NextGridPosition),
            typeof(DynamicCollidable),
            typeof(FollowTarget),
            typeof(MoveRandomly),
            typeof(Health),
            typeof(Damage),
            typeof(TurnsUntilActive),
            typeof(CharacterColor)
        );
        ZombieArchetype = World.DefaultGameObjectInjectionWorld.EntityManager.CreateArchetype(
            typeof(Zombie),
            typeof(GridPosition),
            typeof(NextGridPosition),
            typeof(DynamicCollidable),
            typeof(MoveTowardsTarget),
            typeof(Health),
            typeof(Damage),
            typeof(TurnsUntilActive),
            typeof(CharacterColor)
        );

        AudibleArchetype = World.DefaultGameObjectInjectionWorld.EntityManager.CreateArchetype(typeof(Audible));
    }

}
