using Unity.Entities;
using Unity.Rendering;
using Unity.Transforms;
using UnityEngine;

public sealed class Bootstrap
{
    public static EntityArchetype HumanArchetype;
    public static EntityArchetype ZombieArchetype;

    public static int HumanStartingHealth = 100;
    public static int HumanDamage = 0;
    public static MeshInstanceRenderer HumanMeshInstanceRenderer;
    public static int ZombieVisionDistance = 4;
    public static int ZombieStartingHealth = 70;
    public static int ZombieDamage = 20;
    public static MeshInstanceRenderer ZombieMeshInstanceRenderer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        var entityManager = World.Active.GetOrCreateManager<EntityManager>();

        HumanArchetype = entityManager.CreateArchetype(
            typeof(Human),
            typeof(Position),
            typeof(GridPosition),
            typeof(Collidable),
            typeof(Movable),
            typeof(Health),
            typeof(Damage)
        );
        ZombieArchetype = entityManager.CreateArchetype(
            typeof(Zombie),
            typeof(Position),
            typeof(GridPosition),
            typeof(PrevMoveDirection),
            typeof(Collidable),
            typeof(Movable),
            typeof(Health),
            typeof(Damage)
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeWithScene()
    { 
        HumanMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("HumanRenderPrototype");
        ZombieMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("ZombieRenderPrototype");
    }

    private static MeshInstanceRenderer GetMeshInstanceRendererFromPrototype(string protoName)
    {
        var proto = GameObject.Find(protoName);
        var result = proto.GetComponent<MeshInstanceRendererComponent>().Value;
        Object.Destroy(proto);
        return result;
    }
}
