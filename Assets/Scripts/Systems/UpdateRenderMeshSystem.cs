using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

[UpdateAfter(typeof(DamageSystem))]
public class UpdateRenderMeshSystem : ComponentSystem
{
    private EntityQuery m_Humans;
    private EntityQuery m_Zombies;

    protected override void OnUpdate()
    {
        var humanEntityArray = m_Humans.ToEntityArray(Allocator.TempJob);
        var humanHealthRangeArray = m_Humans.ToComponentDataArray<HealthRange>(Allocator.TempJob);

        var zombieEntityArray = m_Zombies.ToEntityArray(Allocator.TempJob);
        var zombieHealthRangeArray = m_Zombies.ToComponentDataArray<HealthRange>(Allocator.TempJob);

        for (int i = 0; i < humanEntityArray.Length; i++)
        {
            if (humanHealthRangeArray[i].Value == 75)
            { 
                PostUpdateCommands.RemoveComponent(humanEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(humanEntityArray[i], Bootstrap.HumanMeshInstanceRenderer_Health_75);
            }
            if (humanHealthRangeArray[i].Value == 50)
            {
                PostUpdateCommands.RemoveComponent(humanEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(humanEntityArray[i], Bootstrap.HumanMeshInstanceRenderer_Health_50);
            }
            if (humanHealthRangeArray[i].Value == 25)
            {
                PostUpdateCommands.RemoveComponent(humanEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(humanEntityArray[i], Bootstrap.HumanMeshInstanceRenderer_Health_25);
            }
        }

        for (int i = 0; i < zombieEntityArray.Length; i++)
        {
            if (zombieHealthRangeArray[i].Value == 75)
            {
                PostUpdateCommands.RemoveComponent(zombieEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(zombieEntityArray[i], Bootstrap.ZombieMeshInstanceRenderer_Health_75);
            }
            if (zombieHealthRangeArray[i].Value == 50)
            {
                PostUpdateCommands.RemoveComponent(zombieEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(zombieEntityArray[i], Bootstrap.ZombieMeshInstanceRenderer_Health_50);
            }
            if (zombieHealthRangeArray[i].Value == 25)
            {
                PostUpdateCommands.RemoveComponent(zombieEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(zombieEntityArray[i], Bootstrap.ZombieMeshInstanceRenderer_Health_25);
            }
        }

        humanEntityArray.Dispose();
        humanHealthRangeArray.Dispose();

        zombieEntityArray.Dispose();
        zombieHealthRangeArray.Dispose();
    }

    protected override void OnCreate()
    {
        m_Humans = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(HealthRange)),
            typeof(RenderMesh)
        );
        m_Humans.SetFilterChanged(typeof(HealthRange));

        m_Zombies = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Zombie)),
            ComponentType.ReadOnly(typeof(HealthRange)),
            typeof(RenderMesh)
        );
        m_Zombies.SetFilterChanged(typeof(HealthRange));
    }
}
