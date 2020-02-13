using Unity.Collections;
using Unity.Entities;
using Unity.Rendering;

[UpdateInGroup(typeof(EndGroup))]
public class UpdateRenderMeshSystem : ComponentSystem
{
    private EntityQuery m_Humans;
    private EntityQuery m_Zombies;

    protected override void OnCreate()
    {
        m_Humans = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Human)),
            ComponentType.ReadOnly(typeof(HealthRange)),
            typeof(RenderMesh)
        );
        m_Humans.AddChangedVersionFilter(typeof(HealthRange));

        m_Zombies = GetEntityQuery(
            ComponentType.ReadOnly(typeof(Zombie)),
            ComponentType.ReadOnly(typeof(HealthRange)),
            typeof(RenderMesh)
        );
        m_Zombies.AddChangedVersionFilter(typeof(HealthRange));
    }

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
                PostUpdateCommands.AddSharedComponent(humanEntityArray[i], GameController.instance.Human_75_RenderMesh);
            }
            if (humanHealthRangeArray[i].Value == 50)
            {
                PostUpdateCommands.RemoveComponent(humanEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(humanEntityArray[i], GameController.instance.Human_50_RenderMesh);
            }
            if (humanHealthRangeArray[i].Value == 25)
            {
                PostUpdateCommands.RemoveComponent(humanEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(humanEntityArray[i], GameController.instance.Human_25_RenderMesh);
            }
        }

        for (int i = 0; i < zombieEntityArray.Length; i++)
        {
            if (zombieHealthRangeArray[i].Value == 75)
            {
                PostUpdateCommands.RemoveComponent(zombieEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(zombieEntityArray[i], GameController.instance.Zombie_75_RenderMesh);
            }
            if (zombieHealthRangeArray[i].Value == 50)
            {
                PostUpdateCommands.RemoveComponent(zombieEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(zombieEntityArray[i], GameController.instance.Zombie_50_RenderMesh);
            }
            if (zombieHealthRangeArray[i].Value == 25)
            {
                PostUpdateCommands.RemoveComponent(zombieEntityArray[i], typeof(RenderMesh));
                PostUpdateCommands.AddSharedComponent(zombieEntityArray[i], GameController.instance.Zombie_25_RenderMesh);
            }
        }

        humanEntityArray.Dispose();
        humanHealthRangeArray.Dispose();

        zombieEntityArray.Dispose();
        zombieHealthRangeArray.Dispose();
    }
}
