using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using Unity.Rendering;
using UnityEngine;

public sealed class Bootstrap
{
    public static EntityArchetype BuildingTileArchetype;
    public static EntityArchetype RoadFloorArchetype;

    public static EntityArchetype HumanArchetype;
    public static EntityArchetype ZombieArchetype;

    public static EntityArchetype AudibleArchetype;

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
    public static int ZombieVisionDistance = 4;
    public static int ZombieHearingDistance = 12;
    public static int ZombieStartingHealth = 70;
    public static int ZombieDamage = 20;
    public static RenderMesh ZombieMeshInstanceRenderer;

    private static EntityManager _entityManager;
    private static bool[,] _tileExists;
    private static bool[,] _tilePassable;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    public static void Initialize()
    {
        _entityManager = World.Active.EntityManager;

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
            typeof(DynamicCollidable),
            typeof(FollowTarget),
            typeof(MoveRandomly),
            typeof(Health),
            typeof(Damage)
        );
        ZombieArchetype = _entityManager.CreateArchetype(
            typeof(Zombie),
            typeof(LocalToWorld),
            typeof(Translation),
            typeof(GridPosition),
            typeof(DynamicCollidable),
            typeof(MoveTowardsTarget),
            typeof(Health),
            typeof(Damage)
        );

        AudibleArchetype = _entityManager.CreateArchetype(
            typeof(GridPosition),
            typeof(Audible)
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
            do
            {
                randomX = UnityEngine.Random.Range(1, numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, numTilesY - 1);
            } while (!_tilePassable[randomY, randomX]);

            AddHumanCharacter(randomX, randomY);
        }

        for (int i = 0; i < numZombies; i++)
        {
            // Place zombie in random place
            do
            {
                randomX = UnityEngine.Random.Range(1, numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, numTilesY - 1);
            } while (!_tilePassable[randomY, randomX]);

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

        CreateStreets();

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

    private static void CreateStreets()
    {
        int roadSize;
        int xPos, yPos;

        // Vertical Streets
        xPos = 1;
        for (int i = 0; i < numStreets / 2; i++)
        {
            roadSize = UnityEngine.Random.Range(1, 3);

            xPos += UnityEngine.Random.Range(0, 2 * (numTilesX / (numStreets / 2)));
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

            yPos += UnityEngine.Random.Range(0, 2 * (numTilesY / (numStreets / 2)));
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
        _entityManager.SetComponentData(entity, new Translation { Value = new float3((float)numTilesX / 2 + 0.5f, 0f, (float)numTilesY / 2 + 0.5f) });
        _entityManager.SetComponentData(entity, new NonUniformScale { Value = new float3(numTilesX - 0.5f, 1f, numTilesY - 0.5f) });
        _entityManager.AddSharedComponentData(entity, RoadTileMeshInstanceRenderer);
    }

    private static void AddBuildingTile(int x, int y, int z, bool gridPosition)
    {
        Entity entity = _entityManager.CreateEntity(BuildingTileArchetype);
        _entityManager.SetComponentData(entity, new Translation { Value = new float3(x, y, z) });
        if (gridPosition)
            _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, y, z) });
        _entityManager.AddSharedComponentData(entity, BuildingTileMeshInstanceRenderer);

        _tileExists[y, x] = true;
        _tilePassable[y, x] = false;
    }
    private static void AddHumanCharacter(int x, int y)
    {
        Entity entity = _entityManager.CreateEntity(HumanArchetype);
        _entityManager.SetComponentData(entity, new Translation { Value = new float3(x, 1f, y) });
        _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new Health { Value = HumanStartingHealth });
        _entityManager.SetComponentData(entity, new Damage { Value = HumanDamage });
        _entityManager.AddSharedComponentData(entity, HumanMeshInstanceRenderer);

        _tilePassable[y, x] = false;
    }

    private static void AddZombieCharacter(int x, int y)
    {
        Entity entity = _entityManager.CreateEntity(ZombieArchetype);
        _entityManager.SetComponentData(entity, new Translation { Value = new float3(x, 1f, y) });
        _entityManager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        _entityManager.SetComponentData(entity, new MoveTowardsTarget { TurnsSinceMove = 0 });
        _entityManager.SetComponentData(entity, new Health { Value = ZombieStartingHealth });
        _entityManager.SetComponentData(entity, new Damage { Value = ZombieDamage });
        _entityManager.AddSharedComponentData(entity, ZombieMeshInstanceRenderer);

        _tilePassable[y, x] = false;
    }
}
