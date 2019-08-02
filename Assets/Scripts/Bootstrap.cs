using System.Collections.Generic;
using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = Unity.Mathematics.Random;

public sealed class Bootstrap
{
    public static EntityArchetype BuildingTileArchetype;
    public static EntityArchetype RoadFloorArchetype;

    public static EntityArchetype HumanArchetype;
    public static EntityArchetype ZombieArchetype;

    public static EntityArchetype AudibleArchetype;

    public static RenderMesh BuildingTileMeshInstanceRenderer;
    public static RenderMesh RoadTileMeshInstanceRenderer;
    
    public static RenderMesh HumanMeshInstanceRenderer_Health_Full;
    public static RenderMesh HumanMeshInstanceRenderer_Health_75;
    public static RenderMesh HumanMeshInstanceRenderer_Health_50;
    public static RenderMesh HumanMeshInstanceRenderer_Health_25;

    public static RenderMesh ZombieMeshInstanceRenderer_Health_Full;
    public static RenderMesh ZombieMeshInstanceRenderer_Health_75;
    public static RenderMesh ZombieMeshInstanceRenderer_Health_50;
    public static RenderMesh ZombieMeshInstanceRenderer_Health_25;

    private static EntityManager _entityManager;
    private static List<Entity> _cityEntities;
    private static List<Entity> _unitEntities;

    private static bool[,] _tileExists;
    private static bool[,] _tilePassable;

    private static Random rand;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        _entityManager = World.Active.EntityManager;
        _cityEntities = new List<Entity>();
        _unitEntities = new List<Entity>();

        rand = new Random();
        rand.InitState();

        BuildingTileArchetype = _entityManager.CreateArchetype(
            typeof(LocalToWorld),
            typeof(Translation),
            typeof(GridPosition),
            typeof(StaticCollidable)
        );

        RoadFloorArchetype = _entityManager.CreateArchetype(
            typeof(LocalToWorld),
            typeof(Translation),
            typeof(NonUniformScale)
        );

        HumanArchetype = _entityManager.CreateArchetype(
            typeof(Human),
            typeof(LocalToWorld),
            typeof(Translation),
            typeof(GridPosition),
            typeof(NextGridPosition),
            typeof(DynamicCollidable),
            typeof(FollowTarget),
            typeof(MoveRandomly),
            typeof(Health),
            typeof(HealthRange),
            typeof(Damage),
            typeof(TurnsUntilMove)
        );
        ZombieArchetype = _entityManager.CreateArchetype(
            typeof(Zombie),
            typeof(LocalToWorld),
            typeof(Translation),
            typeof(GridPosition),
            typeof(NextGridPosition),
            typeof(DynamicCollidable),
            typeof(MoveTowardsTarget),
            typeof(Health),
            typeof(HealthRange),
            typeof(Damage),
            typeof(TurnsUntilMove)
        );

        AudibleArchetype = _entityManager.CreateArchetype(
            typeof(Audible)
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeWithScene()
    {
        BuildingTileMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("BuildingTileRenderPrototype");
        RoadTileMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("RoadTileRenderPrototype");

        HumanMeshInstanceRenderer_Health_Full = GetMeshInstanceRendererFromPrototype("HumanRenderPrototype_Health_Full");
        HumanMeshInstanceRenderer_Health_75 = GetMeshInstanceRendererFromPrototype("HumanRenderPrototype_Health_75");
        HumanMeshInstanceRenderer_Health_50 = GetMeshInstanceRendererFromPrototype("HumanRenderPrototype_Health_50");
        HumanMeshInstanceRenderer_Health_25 = GetMeshInstanceRendererFromPrototype("HumanRenderPrototype_Health_25");

        ZombieMeshInstanceRenderer_Health_Full = GetMeshInstanceRendererFromPrototype("ZombieRenderPrototype_Health_Full");
        ZombieMeshInstanceRenderer_Health_75 = GetMeshInstanceRendererFromPrototype("ZombieRenderPrototype_Health_75");
        ZombieMeshInstanceRenderer_Health_50 = GetMeshInstanceRendererFromPrototype("ZombieRenderPrototype_Health_50");
        ZombieMeshInstanceRenderer_Health_25 = GetMeshInstanceRendererFromPrototype("ZombieRenderPrototype_Health_25");

        int numTilesX = GameController.instance.numTilesX;
        int numTilesY = GameController.instance.numTilesY;

        // Instantiate city tiles
        Regenerate(numTilesX, numTilesY);
    }

    public static void Regenerate(int numTilesX, int numTilesY)
    {
        _cityEntities.ForEach(delegate(Entity entity)
        {
            _entityManager.DestroyEntity(entity);
        });
        _cityEntities.Clear();

        _unitEntities.ForEach(delegate (Entity entity)
        {
            _entityManager.DestroyEntity(entity);
        });
        _unitEntities.Clear();

        _tileExists = new bool[numTilesY, numTilesX];
        _tilePassable = new bool[numTilesY, numTilesX];

        // Line the grid with impassable tiles
        for (int y = 0; y < numTilesY; y++)
        {
            AddBuildingTile(0, 0, y, false);
            AddBuildingTile(0, 1, y, true);

            AddBuildingTile(numTilesX - 1, 0, y, false);
            AddBuildingTile(numTilesX - 1, 1, y, true);
        }
        for (int x = 1; x < numTilesX - 1; x++)
        {
            AddBuildingTile(x, 0, 0, false);
            AddBuildingTile(x, 1, 0, true);
            AddBuildingTile(x, 0, numTilesY - 1, false);
            AddBuildingTile(x, 1, numTilesY - 1, true);
        }

        CreateStreets(GameController.instance.numStreets, numTilesX, numTilesY);

        // Fill in empty space with building tiles
        for (int y = 1; y < numTilesY - 1; y++)
        {
            for (int x = 1; x < numTilesX - 1; x++)
            {
                if (!_tileExists[y, x])
                {
                    AddBuildingTile(x, 1, y, true);
                }
            }
        }

        int randomX, randomY;
        for (int i = 0; i < GameController.instance.numHumans; i++)
        {
            // Place human in random place
            do
            {
                randomX = UnityEngine.Random.Range(1, numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, numTilesY - 1);
            } while (!_tilePassable[randomY, randomX]);

            AddHumanCharacter(randomX, randomY, GameController.instance.humanStartingHealth, GameController.instance.humanDamage, GameController.instance.humanTurnDelay);
        }
        for (int i = 0; i < GameController.instance.numZombies; i++)
        {
            // Place zombie in random place
            do
            {
                randomX = UnityEngine.Random.Range(1, numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, numTilesY - 1);
            } while (!_tilePassable[randomY, randomX]);

            AddZombieCharacter(randomX, randomY, GameController.instance.zombieStartingHealth, GameController.instance.zombieDamage, GameController.instance.zombieTurnDelay);
        }
    }

    private static void CreateStreets(int numStreets, int numTilesX, int numTilesY)
    {
        int roadSize;
        int xPos, yPos;

        // Vertical Streets
        xPos = 1;
        for (int i = 0; i < numStreets / 2; i++)
        {
            roadSize = UnityEngine.Random.Range(1, 3);

            xPos = xPos + UnityEngine.Random.Range(0, 2 * (numTilesX / (numStreets / 2)));
            if (xPos >= numTilesX - 1)
                break;

            while (roadSize >= 1 && xPos <= numTilesX - 1)
            {
                for (yPos = 1; yPos < numTilesY - 1; yPos++)
                {
                    if (!_tileExists[yPos, xPos])
                    {
                        _tileExists[yPos, xPos] = true;
                        _tilePassable[yPos, xPos] = true;
                    }
                }
                xPos++;
                roadSize--;
            }
        }

        // Horizontal Streets
        yPos = 1;
        for (int i = 0; i < numStreets / 2; i++)
        {
            roadSize = UnityEngine.Random.Range(1, 3);

            yPos = yPos + UnityEngine.Random.Range(0, 2 * (numTilesY / (numStreets / 2)));
            if (yPos >= numTilesY - 1)
                break;

            while (roadSize >= 1 && yPos <= numTilesY - 1)
            {
                for (xPos = 1; xPos < numTilesX - 1; xPos++)
                {
                    if (!_tileExists[yPos, xPos])
                    {
                        _tileExists[yPos, xPos] = true;
                        _tilePassable[yPos, xPos] = true;
                    }
                }
                yPos++;
                roadSize--;
            }
        }

        Entity entity = _entityManager.CreateEntity(RoadFloorArchetype);
        _entityManager.SetComponentData(entity, new Translation { Value = new float3((float)numTilesX / 2 - 0.5f, 0f, (float)numTilesY / 2 - 0.5f) });
        _entityManager.SetComponentData(entity, new NonUniformScale { Value = new float3(numTilesX - 0.5f, 1f, numTilesY - 0.5f) });
        _entityManager.AddSharedComponentData(entity, RoadTileMeshInstanceRenderer);
        _cityEntities.Add(entity);
    }

    private static void AddBuildingTile(int x, int y, int z, bool gridPosition)
    {
        Entity entity = _entityManager.CreateEntity(BuildingTileArchetype);
        _entityManager.SetComponentData(entity, new Translation { Value = new float3(x, y, z) });
        if (gridPosition)
            _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, y, z) });
        _entityManager.AddSharedComponentData(entity, BuildingTileMeshInstanceRenderer);
        _cityEntities.Add(entity);

        _tileExists[y, x] = true;
        _tilePassable[y, x] = false;
    }
    public static void AddHumanCharacter(int x, int y, int health, int damage, int turnDelay)
    {
        Entity entity = _entityManager.CreateEntity(HumanArchetype);
        _entityManager.SetComponentData(entity, new Translation { Value = new float3(x, 1f, y) });
        _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new NextGridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new Health { Value = health });
        _entityManager.SetComponentData(entity, new HealthRange { Value = 100 });
        _entityManager.SetComponentData(entity, new Damage { Value = damage });
        _entityManager.SetComponentData(entity, new TurnsUntilMove { Value = rand.NextInt(turnDelay + 1) });
        _entityManager.AddSharedComponentData(entity, HumanMeshInstanceRenderer_Health_Full);
        _unitEntities.Add(entity);

        _tilePassable[y, x] = false;
    }

    public static void AddZombieCharacter(int x, int y, int health, int damage, int turnDelay)
    {
        Entity entity = _entityManager.CreateEntity(ZombieArchetype);
        _entityManager.SetComponentData(entity, new Translation { Value = new float3(x, 1f, y) });
        _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new NextGridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new Health { Value = health });
        _entityManager.SetComponentData(entity, new HealthRange { Value = 100 });
        _entityManager.SetComponentData(entity, new Damage { Value = damage });
        _entityManager.SetComponentData(entity, new TurnsUntilMove { Value = rand.NextInt(turnDelay + 1) });
        _entityManager.AddSharedComponentData(entity, ZombieMeshInstanceRenderer_Health_Full);
        _unitEntities.Add(entity);

        _tilePassable[y, x] = false;
    }

    private static RenderMesh GetMeshInstanceRendererFromPrototype(string protoName)
    {
        var proto = GameObject.Find(protoName);
        var result = proto.GetComponent<RenderMeshProxy>().Value;
        Object.Destroy(proto);
        return result;
    }
}
