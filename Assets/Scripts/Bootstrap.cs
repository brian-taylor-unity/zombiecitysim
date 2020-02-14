﻿//using Unity.Entities;
//using Unity.Transforms;
//using UnityEngine;

//public sealed class Bootstrap
//{
//    public static EntityArchetype HumanArchetype;
//    public static EntityArchetype ZombieArchetype;

//    public static EntityArchetype AudibleArchetype;

//    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
//    public static void Initialize()
//    {
//        HumanArchetype = _entityManager.CreateArchetype(
//            typeof(Human),
//            typeof(LocalToWorld),
//            typeof(Translation),
//            typeof(GridPosition),
//            typeof(NextGridPosition),
//            typeof(DynamicCollidable),
//            typeof(FollowTarget),
//            typeof(MoveRandomly),
//            typeof(Health),
//            typeof(HealthRange),
//            typeof(Damage),
//            typeof(TurnsUntilActive)
//        );
//        ZombieArchetype = _entityManager.CreateArchetype(
//            typeof(Zombie),
//            typeof(LocalToWorld),
//            typeof(Translation),
//            typeof(GridPosition),
//            typeof(NextGridPosition),
//            typeof(DynamicCollidable),
//            typeof(MoveTowardsTarget),
//            typeof(Health),
//            typeof(HealthRange),
//            typeof(Damage),
//            typeof(TurnsUntilActive)
//        );

//        AudibleArchetype = _entityManager.CreateArchetype(
//            typeof(Audible)
//        );
//    }
//}
