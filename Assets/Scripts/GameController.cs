using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    private EntityManager manager;

    public static GameController instance = null;

    public int numTilesX = 130;
    public int numTilesY = 130;
    public int numHumans;
    public int numZombies;

    public City cityPrefab;

    public GameObject humans;
    public GameObject zombies;
    public GameObject humanCharacterPrefab;
    public GameObject zombieCharacterPrefab;

    private City cityInstance;
    private List<GameObject> humanCharacters;
    private List<GameObject> zombieCharacters;

    private int numSteps = 0;
    private bool finished = false;

    void Awake()
    {
        manager = World.Active.GetOrCreateManager<EntityManager>();

        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(gameObject);

        DontDestroyOnLoad(gameObject);

        humanCharacters = new List<GameObject>();
        zombieCharacters = new List<GameObject>();

        cityPrefab.numTilesX = numTilesX;
        cityPrefab.numTilesY = numTilesY;
        cityInstance = Instantiate(cityPrefab);
        cityInstance.transform.SetParent(transform, false);
    }

    // Use this for initialization
    void Start()
    {
        int randomX, randomY;

        for (int i = 0; i < numHumans; i++)
        {
            // Place human in random place
            do
            {
                randomX = UnityEngine.Random.Range(1, cityInstance.numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, cityInstance.numTilesY - 1);
            } while (!IsPassable(randomX, randomY));

            AddHumanCharacter(randomX, randomY);
        }

        for (int i = 0; i < numZombies; i++)
        {
            // Place zombie in random place
            do
            {
                randomX = UnityEngine.Random.Range(1, cityInstance.numTilesX - 1);
                randomY = UnityEngine.Random.Range(1, cityInstance.numTilesY - 1);
            } while (!IsPassable(randomX, randomY));

            AddZombieCharacter(randomX, randomY);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //// Move characters
        //foreach (GameObject human in humanCharacters)
        //{
        //    human.GetComponent<HumanCharacter>().Move();
        //}

        //foreach (GameObject zombie in zombieCharacters)
        //{
        //    zombie.GetComponent<ZombieCharacter>().Move();
        //}

        if (!finished && humanCharacters.Count == 0)
        {
            Debug.Log("numSteps: " + numSteps);
            finished = true;
        }

        numSteps++;
    }

    public void AddHumanCharacter(int x, int y)
    {
        Entity entity = manager.Instantiate(humanCharacterPrefab);
        manager.SetComponentData(entity, new Position { Value = new float3(x, 1f, y) });
        manager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        cityInstance.SetPassable(false, x, y);

        //HumanCharacter humanInstance = Instantiate(humanCharacterPrefab);
        //humanInstance.transform.SetParent(humans.transform);

        //humanInstance.Move(x, y);
        //humanCharacters.Add(humanInstance.gameObject);
    }

    public void AddZombieCharacter(int x, int y)
    {
        Entity entity = manager.Instantiate(zombieCharacterPrefab);
        manager.SetComponentData(entity, new Position { Value = new float3(x, 1f, y) });
        manager.SetComponentData(entity, new GridPosition { Value = new int3(x, 1, y) });
        cityInstance.SetPassable(false, x, y);

        //ZombieCharacter zombieInstance = Instantiate(zombieCharacterPrefab);
        //zombieInstance.transform.SetParent(zombies.transform);

        //zombieInstance.Move(x, y);
        //zombieCharacters.Add(zombieInstance.gameObject);
    }

    public void RemoveHumanCharacter(GameObject human)
    {
        humanCharacters.Remove(human);
        Destroy(human);
    }

    public void RemoveZombieCharacter(GameObject zombie)
    {
        zombieCharacters.Remove(zombie);
        Destroy(zombie);
    }

    public bool IsPassable(int x, int y)
    {
        bool passable = true;
        foreach (GameObject obj in humanCharacters)
        {
            HumanCharacter human = obj.GetComponent<HumanCharacter>();
            if (human.x == x && human.y == y)
            {
                passable = false;
                break;
            }
        }

        foreach (GameObject obj in zombieCharacters)
        {
            ZombieCharacter zombie = obj.GetComponent<ZombieCharacter>();
            if (zombie.x == x && zombie.y == y)
            {
                passable = false;
                break;
            }
        }

        return passable && cityInstance.IsPassable(x, y);
    }
}
