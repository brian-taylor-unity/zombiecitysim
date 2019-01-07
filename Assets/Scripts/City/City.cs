using Unity.Entities;
using Unity.Transforms;
using Unity.Mathematics;
using UnityEngine;

public class City : MonoBehaviour
{
    EntityManager manager;

    public int numTilesX;
    public int numTilesY;
    public int unitsPerTile = 1;
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
        //return tileGrid.IsPassable(x, y);
        return tilePassable[y, x];
    }

    public void SetPassable(bool passable, int x, int y)
    {
        tilePassable[y, x] = passable;
    }

    private void AddTile(GameObject tile, int x, int y)
    {
        //Tile tileInstance = Instantiate(tile, new Vector3(x, y, transform.position.z), Quaternion.identity);
        //tileInstance.transform.SetParent(transform);
        //tileGrid.AddTile(tileInstance, x, y);

        Entity entity = manager.Instantiate(tile);
        manager.SetComponentData(entity, new Position { Value = new float3(x, 0f, y) });
        manager.SetComponentData(entity, new GridPosition { Value = new int3(x, 0, y) });
        if (tile == buildingTilePrefab)
        {
            entity = manager.Instantiate(tile);
            manager.SetComponentData(entity, new Position { Value = new float3(x, 1f, y) });
            manager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });

            tileExists[y, x] = true;
            tilePassable[y, x] = false;
        }
        if (tile == roadTilePrefab)
        {
            tileExists[y, x] = true;
            tilePassable[y, x] = true;
        }
    }

    private void Generate()
    {
        // tileGrid = new TileGrid(numTilesX, numTilesY);
        tileExists = new bool[numTilesY, numTilesX];
        tilePassable = new bool[numTilesY, numTilesX];

        // Line the grid with impassable tiles
        for (int y = 0; y < numTilesY; y++)
        {
            AddTile(buildingTilePrefab, 0, y);
            AddTile(buildingTilePrefab, numTilesX - 1, y);
        }
        for (int x = 1; x < numTilesX - 1; x++)
        {
            AddTile(buildingTilePrefab, x, 0);
            AddTile(buildingTilePrefab, x, numTilesY - 1);
        }

        // Randomly create tiles
        //for (int y = 1; y < numTilesY - 1; y++)
        //{
        //    for (int x = 1; x < numTilesX - 1; x++)
        //    {
        //        randomTile = Random.Range(0.0f, 1.0f);
        //        tile = Instantiate((randomTile < 0.5f ? roadTile : buildingTile), new Vector3(x, y), Quaternion.identity) as Transform;
        //        tile.parent = transform;
        //    }
        //}

        CreateStreets();

        // Fill in empty space with building tiles
        for (int y = 1; y < numTilesY - 1; y++)
        {
            for (int x = 1; x < numTilesX - 1; x++)
            {
                if (!tileExists[y, x])
                {
                    AddTile(buildingTilePrefab, x, y);
                }
            }
        }

        //for (int numBuildings = numTilesX * numTilesY / 10; numBuildings > 0; numBuildings--)
        //{
        //    CreateBuilding(numTilesX / 10, numTilesY / 10);
        //}
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
                        AddTile(roadTilePrefab, xPos, yPos);
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
                        AddTile(roadTilePrefab, xPos, yPos);
                    }
                }
                yPos++;
                roadSize--;
            }
        }

    }

    private void CreateBuilding(int width, int height)
    {

    }
}
