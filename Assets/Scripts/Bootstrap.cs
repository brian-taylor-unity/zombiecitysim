using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;
using Random = UnityEngine.Random;

public sealed class Bootstrap
{
    public static EntityArchetype BuildingTileArchetype;
    public static EntityArchetype RoadFloorArchetype;

    public static EntityArchetype HumanArchetype;
    public static EntityArchetype ZombieArchetype;

    public static int numTilesX;
    public static int numTilesY;
    public static int numStreets;
    public static int numHumans;
    public static int numZombies;

    /// <summary>
    /// Building Tile definitions
    /// </summary>
    public static RenderMesh BuildingTileMeshInstanceRenderer;

    /// <summary>
    /// Building Tile definitions
    /// </summary>
    public static RenderMesh RoadTileMeshInstanceRenderer;

    /// <summary>
    /// Human definitions
    /// </summary>
    public static int HumanStartingHealth = 100;
    public static int HumanDamage = 0;
    public static RenderMesh HumanMeshInstanceRenderer;

    /// <summary>
    /// Zombie definitions
    /// </summary>
    public static int ZombieVisionDistance;
    public static int ZombieStartingHealth = 70;
    public static int ZombieDamage = 20;
    public static RenderMesh ZombieMeshInstanceRenderer;

    private static EntityManager _entityManager;
    private static bool[,] _tileExists;
    private static bool[,] _tilePassable;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        _entityManager = World.Active.GetOrCreateManager<EntityManager>();

        BuildingTileArchetype = _entityManager.CreateArchetype(
            typeof(Position),
            typeof(GridPosition),
            typeof(StaticCollidable)
        );

        RoadFloorArchetype = _entityManager.CreateArchetype(
            typeof(Position),
            typeof(Scale)
        );

        HumanArchetype = _entityManager.CreateArchetype(
            typeof(Human),
            typeof(Position),
            typeof(GridPosition),
            typeof(DynamicCollidable),
            typeof(FollowTarget),
            typeof(MoveRandomly),
            typeof(Health),
            typeof(Damage)
        );
        ZombieArchetype = _entityManager.CreateArchetype(
            typeof(Zombie),
            typeof(Position),
            typeof(GridPosition),
            typeof(DynamicCollidable),
            typeof(MoveFollowTarget),
            typeof(Health),
            typeof(Damage)
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeWithScene()
    {
        numTilesX = GameController.instance.numTilesX;
        numTilesY = GameController.instance.numTilesY;
        numStreets = GameController.instance.numStreets;
        numHumans = GameController.instance.numHumans;
        numZombies = GameController.instance.numZombies;
        ZombieVisionDistance = GameController.instance.zombieVisionDistance;

        BuildingTileMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("BuildingTileRenderPrototype");
        RoadTileMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("RoadTileRenderPrototype");
        HumanMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("HumanRenderPrototype");
        ZombieMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("ZombieRenderPrototype");

        // Instantiate city tiles
        Generate();

        int randomX, randomY;

        for (int i = 0; i < numHumans; i++)
        {
            // Place human in random place
            int attempts = 0;
            do
            {
                randomX = UnityEngine.Random.Range(1, numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, numTilesY - 1);
                attempts++;
            } while (!_tilePassable[randomY, randomX] && attempts < numTilesX * numTilesY);

            AddHumanCharacter(randomX, randomY);
        }

        for (int i = 0; i < numZombies; i++)
        {
            // Place zombie in random place
            int attempts = 0;
            do
            {
                randomX = UnityEngine.Random.Range(1, numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, numTilesY - 1);
            } while (!_tilePassable[randomY, randomX] && attempts < numTilesX * numTilesY);

            AddZombieCharacter(randomX, randomY);
        }
    }

    private static RenderMesh GetMeshInstanceRendererFromPrototype(string protoName)
    {
        var proto = GameObject.Find(protoName);
        var result = proto.GetComponent<RenderMeshProxy>().Value;
        Object.Destroy(proto);
        return result;
    }

    private static void Generate()
    {
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

        CreateMajorStreets();

        //CreateBuildings();

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
    }

    private static void CreateMajorStreets()
    {
        float percentMajor = Random.Range(0.5f, 0.7f);
        int numMajorStreets = (int) (numStreets * percentMajor);

        float percentVertical = Random.Range(0.4f, 0.6f);
        int numVerticalStreets = (int) (numMajorStreets * percentVertical);
        int numHorizontalStreets = numMajorStreets - numVerticalStreets;

        for (int verticalStreet = 0; verticalStreet < numVerticalStreets; verticalStreet++)
        {
            int width = Random.Range(4, 8);
            int startX = (numTilesX / numVerticalStreets) * verticalStreet + Random.Range(0, numTilesX / numVerticalStreets);

            for (int y = 1; y < numTilesY; y++)
            {
                for (int x = startX; x < startX + width; x++)
                {
                    if (x >= numTilesX)
                        break;

                    _tileExists[y, x] = true;
                    _tilePassable[y, x] = true;
                }
            }
        }

        for (int horizontalStreet = 0; horizontalStreet < numHorizontalStreets; horizontalStreet++)
        {
            int width = Random.Range(4, 8);
            int startY = (numTilesY / numHorizontalStreets) * horizontalStreet + Random.Range(0, numTilesX / numHorizontalStreets);

            for (int y = startY; y < startY + width; y++)
            {
                if (y >= numTilesY)
                    break;

                for (int x = 1; x < numTilesX; x++)
                {
                    _tileExists[y, x] = true;
                    _tilePassable[y, x] = true;
                }
            }
        }

        Entity entity = _entityManager.CreateEntity(RoadFloorArchetype);
        _entityManager.SetComponentData(entity, new Position { Value = new float3((float)numTilesX / 2 - 0.5f, 0f, (float)numTilesY / 2 - 0.5f) });
        _entityManager.SetComponentData(entity, new Scale { Value = new float3(numTilesX - 0.5f, 1f, numTilesY - 0.5f) });
        _entityManager.AddSharedComponentData(entity, RoadTileMeshInstanceRenderer);
    }

    private static void CreateBuildings()
    {
        int xStart = 0;
        int yStart = 0;
        int xEnd = 0;
        int yEnd = 0;

        // Find area surrounded by roads
        for (int y = 0; y < numTilesY; y++)
        {
            for (int x = 0; x < numTilesX; x++)
            {
                if (!_tileExists[y, x])
                {
                    xStart = x;
                    yStart = y;
                    break;
                }
            }

            if (xStart != 0 && yStart != 0)
                break;
        }

        for (int y = yStart; y < numTilesY; y++)
        {
            for (int x = xStart; x < numTilesX; x++)
            {
                if (_tileExists[y, x])
                {
                    xEnd = x - 1;
                    yEnd = y - 1;
                    break;
                }
            }

            if (xStart != 0 && yStart != 0)
                break;
        }

        for (int y = yStart; y <= yEnd; y++)
        {
            for (int x = xStart; x <= xEnd; x++)
            {
                AddBuildingTile(x, y, 0, true);
            }
        }
    }

    private static void AddBuildingTile(int x, int y, int z, bool gridPosition)
    {
        Entity entity = _entityManager.CreateEntity(BuildingTileArchetype);
        _entityManager.SetComponentData(entity, new Position { Value = new float3(x, y, z) });
        if (gridPosition)
            _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, y, z) });
        _entityManager.AddSharedComponentData(entity, BuildingTileMeshInstanceRenderer);

        _tileExists[y, x] = true;
        _tilePassable[y, x] = false;
    }
    private static void AddHumanCharacter(int x, int y)
    {
        Entity entity = _entityManager.CreateEntity(HumanArchetype);
        _entityManager.SetComponentData(entity, new Position { Value = new float3(x, 1f, y) });
        _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new Health { Value = HumanStartingHealth });
        _entityManager.SetComponentData(entity, new Damage { Value = HumanDamage });
        _entityManager.AddSharedComponentData(entity, HumanMeshInstanceRenderer);

        _tilePassable[y, x] = false;
    }

    private static void AddZombieCharacter(int x, int y)
    {
        Entity entity = _entityManager.CreateEntity(ZombieArchetype);
        _entityManager.SetComponentData(entity, new Position { Value = new float3(x, 1f, y) });
        _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new Health { Value = ZombieStartingHealth });
        _entityManager.SetComponentData(entity, new Damage { Value = ZombieDamage });
        _entityManager.AddSharedComponentData(entity, ZombieMeshInstanceRenderer);

        _tilePassable[y, x] = false;
    }
}
