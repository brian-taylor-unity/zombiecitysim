using UnityEngine;

public class City : MonoBehaviour
{
    public int numTilesX;
    public int numTilesY;
    public int unitsPerTile = 1;
    public int numStreets;
    public Tile roadTilePrefab;
    public Tile buildingTilePrefab;

    private CityGrid tileGrid;

    void Awake()
    {
        tileGrid = new CityGrid(numTilesX, numTilesY);

        // Line the grid with impassable tiles
        for (int y = 0; y < numTilesY; y++)
        {
            Tile tileInstance = Instantiate(buildingTilePrefab, new Vector3(0, y, transform.position.z), Quaternion.identity);
            tileInstance.transform.SetParent(transform);
            tileGrid.AddTile(tileInstance, 0, y);

            tileInstance = Instantiate(buildingTilePrefab, new Vector3(numTilesX - 1, y, transform.position.z), Quaternion.identity);
            tileInstance.transform.SetParent(transform);
            tileGrid.AddTile(tileInstance, numTilesX - 1, y);
        }
        for (int x = 1; x < numTilesX - 1; x++)
        {
            Tile tileInstance = Instantiate(buildingTilePrefab, new Vector3(x, 0, transform.position.z), Quaternion.identity);
            tileInstance.transform.SetParent(transform);
            tileGrid.AddTile(tileInstance, x, 0);

            tileInstance = Instantiate(buildingTilePrefab, new Vector3(x, numTilesY - 1, transform.position.z), Quaternion.identity);
            tileInstance.transform.SetParent(transform);
            tileGrid.AddTile(tileInstance, x, numTilesY - 1);
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
                if (!tileGrid.TileExists(x, y))
                {
                    Tile tileInstance = Instantiate(buildingTilePrefab, new Vector3(x, y, transform.position.z), Quaternion.identity);
                    tileInstance.transform.SetParent(transform);
                    tileGrid.AddTile(tileInstance, x, y);
                }
            }
        }

        //for (int numBuildings = numTilesX * numTilesY / 10; numBuildings > 0; numBuildings--)
        //{
        //    CreateBuilding(numTilesX / 10, numTilesY / 10);
        //}
    }

    void Start()
    {

    }

    void Update()
    {

    }

    void CreateStreets()
    {
        int roadSize;
        int xPos, yPos;

        // Vertical Streets
        xPos = 1;
        for (int i = 0; i < numStreets / 2; i++)
        {
            roadSize = Random.Range(1, 3);

            xPos = xPos + Random.Range(0, 2 * (numTilesX / (numStreets / 2)));
            if (xPos >= numTilesX - 1)
                break;

            while (roadSize >= 1 && xPos <= numTilesX - 1)
            {
                for (yPos = 1; yPos < numTilesY - 1; yPos++)
                {
                    if (!tileGrid.TileExists(xPos, yPos))
                    {
                        Tile tileInstance = Instantiate(roadTilePrefab, new Vector3(xPos, yPos, transform.position.z), Quaternion.identity);
                        tileInstance.transform.SetParent(transform);
                        tileGrid.AddTile(tileInstance, xPos, yPos);
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
            roadSize = Random.Range(1, 3);

            yPos = yPos + Random.Range(0, 2 * (numTilesY / (numStreets / 2)));
            if (yPos >= numTilesY - 1)
                break;

            while (roadSize >= 1 && yPos <= numTilesY - 1)
            {
                for (xPos = 1; xPos < numTilesX - 1; xPos++)
                {
                    if (!tileGrid.TileExists(xPos, yPos))
                    {
                        Tile tileInstance = Instantiate(roadTilePrefab, new Vector3(xPos, yPos, transform.position.z), Quaternion.identity);
                        tileInstance.transform.SetParent(transform);
                        tileGrid.AddTile(tileInstance, xPos, yPos);
                    }
                }
                yPos++;
                roadSize--;
            }
        }

    }

    void CreateBuilding(int width, int height)
    {

    }

    public bool IsPassable(int x, int y)
    {
        return tileGrid.IsPassable(x, y);
    }
}
