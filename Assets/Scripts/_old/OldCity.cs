using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OldCity : MonoBehaviour
{
    public int numTilesX;
    public int numTilesY;
    public int unitsPerTile = 1;
    public int numStreets;
    public Transform roadTile;
    public Transform buildingTile;

    private Transform tileInstance;
    private bool[,] passable;

    private List<Vector3> gridPositions = new List<Vector3>();

    void Awake()
    {
        passable = new bool[numTilesY, numTilesX];

        gridPositions.Clear();

        for (int i = 0; i < numTilesX; i++)
            for (int j = 0; j < numTilesY; j++)
                passable[j, i] = false;

        // Line the grid with impassable tiles
        for (int y = 0; y < numTilesY; y++)
        {
            tileInstance = Instantiate(buildingTile, new Vector3(0, y, transform.position.z), Quaternion.identity) as Transform;
            tileInstance.parent = transform;
            passable[y, 0] = false;
            tileInstance = Instantiate(buildingTile, new Vector3(numTilesX - 1, y, transform.position.z), Quaternion.identity) as Transform;
            tileInstance.parent = transform;
            passable[y, numTilesX - 1] = false;
        }
        for (int x = 0; x < numTilesX; x++)
        {
            tileInstance = Instantiate(buildingTile, new Vector3(x, 0, transform.position.z), Quaternion.identity) as Transform;
            tileInstance.parent = transform;
            passable[0, x] = false;
            tileInstance = Instantiate(buildingTile, new Vector3(x, numTilesY - 1, transform.position.z), Quaternion.identity) as Transform;
            tileInstance.parent = transform;
            passable[numTilesY - 1, x] = false;
        }

        /*
        // Randomly create tiles
        for (int y = 1; y < numTilesY - 1; y++)
        {
            for (int x = 1; x < numTilesX - 1; x++)
            {
                randomTile = Random.Range(0.0f, 1.0f);
                tile = Instantiate((randomTile < 0.5f ? roadTile : buildingTile), new Vector3(x, y), Quaternion.identity) as Transform;
                tile.parent = transform;
            }
        }
        */

        CreateStreets();

        for (int numBuildings = numTilesX * numTilesY / 10; numBuildings > 0; numBuildings--)
        {
            CreateBuilding(numTilesX / 10, numTilesY / 10);
        }


        // Fill in unpassable locations
        for (int i = 0; i < numTilesX; i++)
            for (int j = 0; j < numTilesY; j++)
                if (!passable[j, i])
                {
                    tileInstance = Instantiate(buildingTile, new Vector3(i, j, transform.position.z), Quaternion.identity) as Transform;
                    tileInstance.parent = transform;
                }

        CreateGridPositions();
    }

    void Start()
    {

    }

    void Update()
    {

    }

    void CreateGridPositions()
    {
        RaycastHit hit;
        for (int x = 0; x < numTilesX * unitsPerTile; x++)
        {
            for (int y = 0; y < numTilesY * unitsPerTile; y++)
            {
                if (Physics.Raycast(new Vector3(x, y, 1f), new Vector3(x, y, 0f), out hit, 2f))
                {
                    if (hit.transform.tag == "Impassable")
                        gridPositions.Add(new Vector3(x, y, 0f));
                }
            }
        }


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
                    tileInstance = Instantiate(roadTile, new Vector3(xPos, yPos, transform.position.z), Quaternion.identity) as Transform;
                    tileInstance.parent = transform;
                    passable[yPos, xPos] = true;
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
                    tileInstance = Instantiate(roadTile, new Vector3(xPos, yPos, transform.position.z), Quaternion.identity) as Transform;
                    tileInstance.parent = transform;
                    passable[yPos, xPos] = true;
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
        return passable[y, x];
    }

    public void SetPassable(int x, int y, bool set)
    {
        passable[y, x] = set;
    }
}
