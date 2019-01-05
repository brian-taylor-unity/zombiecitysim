using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    public static GameController instance = null;

    public int numHumans;
    public int numZombies;

    public Transform city;

    public Transform humanCharacterPrefab;
    public Transform zombieCharacterPrefab;

    private Transform prefabInstance;
    private City cityInstance;
    private HumanCharacter humanCharacter;
    private ZombieCharacter zombieCharacter;
    private List<GameObject> humanCharacters;
    private List<GameObject> zombieCharacters;

    private int numSteps = 0;
    private bool finished = false;

    void Awake()
    {
        if (instance == null)
            instance = this;
        else if (instance != this)
            Destroy(gameObject);

        DontDestroyOnLoad(gameObject);
    }

    // Use this for initialization
    void Start()
    {
        int randomX, randomY;

        cityInstance = city.GetComponent<City>();

        humanCharacters = new List<GameObject>();
        zombieCharacters = new List<GameObject>();

        for (int i = 0; i < numHumans; i++)
        {
            // Place human in random place
            do
            {
                randomX = Random.Range(1, cityInstance.numTilesX - 1);
                randomY = Random.Range(1, cityInstance.numTilesY - 1);
            } while (!cityInstance.IsPassable(randomX, randomY));

            AddHumanCharacter(randomX, randomY);
        }

        for (int i = 0; i < numZombies; i++)
        {
            // Place zombie in random place
            do
            {
                randomX = Random.Range(1, cityInstance.numTilesX - 1);
                randomY = Random.Range(1, cityInstance.numTilesY - 1);
            } while (!cityInstance.IsPassable(randomX, randomY));

            AddZombieCharacter(randomX, randomY);
        }
    }

    // Update is called once per frame
    void Update()
    {
        // Move characters
        foreach (GameObject human in humanCharacters)
        {
            humanCharacter = human.GetComponent<HumanCharacter>();
            // humanCharacter.MoveHuman();
        }

        foreach (GameObject zombie in zombieCharacters)
        {
            zombieCharacter = zombie.GetComponent<ZombieCharacter>();
            zombieCharacter.MoveZombie();
        }

        if (humanCharacters.Count == 0 && !finished)
        {
            Debug.Log("numSteps: " + numSteps);
            finished = true;
        }

        numSteps++;
    }

    public void AddHumanCharacter(int x, int y)
    {
        prefabInstance = Instantiate(humanCharacterPrefab, new Vector3(x, y), Quaternion.identity) as Transform;
        prefabInstance.parent = transform;

        humanCharacter = prefabInstance.gameObject.GetComponent<HumanCharacter>();

        humanCharacters.Add(prefabInstance.gameObject);
    }

    public void AddZombieCharacter(int x, int y)
    {
        prefabInstance = Instantiate(zombieCharacterPrefab, new Vector3(x, y), Quaternion.identity) as Transform;
        prefabInstance.parent = transform;

        zombieCharacter = prefabInstance.gameObject.GetComponent<ZombieCharacter>();

        zombieCharacters.Add(prefabInstance.gameObject);
    }

    public void RemoveHumanCharacter(GameObject human)
    {
        // cityInstance.SetPassable((int)human.transform.position.x, (int)human.transform.position.y, true);
        humanCharacters.Remove(human);
        Destroy(human);
    }

    public void RemoveZombieCharacter(GameObject zombie)
    {
        // cityInstance.SetPassable((int)zombie.transform.position.x, (int)zombie.transform.position.y, true);
        zombieCharacters.Remove(zombie);
        Destroy(zombie);
    }
}
