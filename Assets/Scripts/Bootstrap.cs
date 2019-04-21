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
    public static RenderMesh HumanMeshInstanceRenderer;
    public static RenderMesh ZombieMeshInstanceRenderer;

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
            typeof(Damage),
            typeof(TurnsUntilMove)
        );

        AudibleArchetype = _entityManager.CreateArchetype(
            typeof(GridPosition),
            typeof(Audible)
        );
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    public static void InitializeWithScene()
    {
        BuildingTileMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("BuildingTileRenderPrototype");
        RoadTileMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("RoadTileRenderPrototype");
        HumanMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("HumanRenderPrototype");
        ZombieMeshInstanceRenderer = GetMeshInstanceRendererFromPrototype("ZombieRenderPrototype");

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
        float percentMajor = rand.NextFloat(0.5f, 0.7f);
        int numMajorStreets = (int) (numStreets * percentMajor);
        int numMinorStreets = numStreets - numMajorStreets;

        float percentMajorVertical = rand.NextFloat(0.4f, 0.6f);
        int numMajorVerticalStreets = (int) (numMajorStreets * percentMajorVertical);
        int numMajorHorizontalStreets = numMajorStreets - numMajorVerticalStreets;

        float percentMinorVertical = rand.NextFloat(0.3f, 0.7f);
        int numMinorVerticalStreets = (int) (numMinorStreets * percentMinorVertical);
        int numMinorHorizontalStreets = numMinorStreets - numMinorVerticalStreets;

        for (int verticalStreet = 0; verticalStreet < numMajorVerticalStreets; verticalStreet++)
        {
            int width = rand.NextInt(4, 9);
            int startX = (numTilesX / numMajorVerticalStreets) * verticalStreet + rand.NextInt(0, numTilesX / numMajorVerticalStreets + 1);

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

        for (int horizontalStreet = 0; horizontalStreet < numMajorHorizontalStreets; horizontalStreet++)
        {
            int width = rand.NextInt(4, 9);
            int startY = (numTilesY / numMajorHorizontalStreets) * horizontalStreet + rand.NextInt(0, numTilesX / numMajorHorizontalStreets + 1);

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

        for (int verticalStreet = 0; verticalStreet < numMinorVerticalStreets; verticalStreet++)
        {
            int width = rand.NextInt(1, 4);
            int startX = (numTilesX / numMinorVerticalStreets) * verticalStreet + rand.NextInt(0, numTilesX / numMinorVerticalStreets + 1);

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

        for (int horizontalStreet = 0; horizontalStreet < numMinorHorizontalStreets; horizontalStreet++)
        {
            int width = rand.NextInt(1, 4);
            int startY = (numTilesY / numMinorHorizontalStreets) * horizontalStreet + rand.NextInt(0, numTilesX / numMinorHorizontalStreets + 1);

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
        _entityManager.SetComponentData(entity, new Translation { Value = new float3((float)numTilesX / 2 - 0.5f, 0f, (float)numTilesY / 2 - 0.5f) });
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
        _entityManager.SetComponentData(entity, new Damage { Value = damage });
        _entityManager.SetComponentData(entity, new TurnsUntilMove { Value = rand.NextInt(turnDelay + 1) });
        _entityManager.AddSharedComponentData(entity, HumanMeshInstanceRenderer);
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
        _entityManager.SetComponentData(entity, new Damage { Value = damage });
        _entityManager.SetComponentData(entity, new TurnsUntilMove { Value = rand.NextInt(turnDelay + 1) });
        _entityManager.AddSharedComponentData(entity, ZombieMeshInstanceRenderer);
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
