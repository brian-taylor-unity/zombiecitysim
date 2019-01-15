using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public class City : MonoBehaviour
{
    EntityManager manager;

    public int numTilesX;
    public int numTilesY;
    public int numStreets;
    public GameObject roadTilePrefab;
    public GameObject buildingTilePrefab;

    //private TileGrid tileGrid;
    private bool[,] tileExists;
    private bool[,] tilePassable;

    void Awake()
    {
        manager = World.Active.GetOrCreateManager<EntityManager>();
        Generate();
    }

    void Start()
    {
    }

    void Update()
    {
    }

    public bool IsPassable(int x, int y)
    {
        return tilePassable[y, x];
    }

    public void SetPassable(bool passable, int x, int y)
    {
        tilePassable[y, x] = passable;
    }

    private void AddBuildingTile(GameObject tile, int x, int y, int z, bool gridPosition)
    {
        Entity entity = manager.Instantiate(tile);
        entity = manager.Instantiate(tile);
        manager.SetComponentData(entity, new Position { Value = new float3(x, y, z) });
        if (gridPosition)
            manager.SetComponentData(entity, new GridPosition { Value = new int3(x, y, z) });

        tileExists[y, x] = true;
        tilePassable[y, x] = false;
    }

    private void Generate()
    {
        tileExists = new bool[numTilesY, numTilesX];
        tilePassable = new bool[numTilesY, numTilesX];

        // Line the grid with impassable tiles
        for (int y = 0; y < numTilesY; y++)
        {
            AddBuildingTile(buildingTilePrefab, 0, 0, y, false);
            AddBuildingTile(buildingTilePrefab, 0, 1, y, true);

            AddBuildingTile(buildingTilePrefab, numTilesX - 1, 0, y, false);
            AddBuildingTile(buildingTilePrefab, numTilesX - 1, 1, y, true);
        }
        for (int x = 1; x < numTilesX - 1; x++)
        {
            AddBuildingTile(buildingTilePrefab, x, 0, 0, false);
            AddBuildingTile(buildingTilePrefab, x, 1, 0, true);
            AddBuildingTile(buildingTilePrefab, x, 0, numTilesY - 1, false);
            AddBuildingTile(buildingTilePrefab, x, 1, numTilesY - 1, true);
        }

        CreateStreets();

        // Fill in empty space with building tiles
        for (int y = 1; y < numTilesY - 1; y++)
        {
            for (int x = 1; x < numTilesX - 1; x++)
            {
                if (!tileExists[y, x])
                {
                    AddBuildingTile(buildingTilePrefab, x, 1, y, true);
                }
            }
        }
    }

    private void CreateStreets()
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
                    if (!tileExists[yPos, xPos])
                    {
                        tileExists[yPos, xPos] = true;
                        tilePassable[yPos, xPos] = true;
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
                    if (!tileExists[yPos, xPos])
                    {
                        tileExists[yPos, xPos] = true;
                        tilePassable[yPos, xPos] = true;
                    }
                }
                yPos++;
                roadSize--;
            }
        }

        Entity entity = manager.Instantiate(roadTilePrefab);
        manager.SetComponentData(entity, new Position { Value = new float3((float)numTilesX / 2, 0f, (float)numTilesY / 2) });
        manager.SetComponentData(entity, new Scale { Value = new float3(numTilesX, 1f, numTilesY) });
    }
}
