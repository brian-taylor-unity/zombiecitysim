using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class OldGameController : MonoBehaviour
{
    public static OldGameController instance = null;

    public int numHumans;
    public int numZombies;

    public Transform city;

    public Transform humanCharacterPrefab;
    public Transform zombieCharacterPrefab;

    private Transform prefabInstance;
    private OldCity cityInstance;
    private OldHumanCharacter humanCharacter;
    private OldZombieCharacter zombieCharacter;
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

        cityInstance = city.GetComponent<OldCity>();

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
            humanCharacter = human.GetComponent<OldHumanCharacter>();
            humanCharacter.Move();
        }

        foreach (GameObject zombie in zombieCharacters)
        {
            zombieCharacter = zombie.GetComponent<OldZombieCharacter>();
            zombieCharacter.Move();
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
        prefabInstance = Instantiate(humanCharacterPrefab, new Vector3(0, 0), Quaternion.identity) as Transform;
        prefabInstance.parent = transform;

        humanCharacter = prefabInstance.gameObject.GetComponent<OldHumanCharacter>();

        humanCharacter.Move(x, y);

        humanCharacters.Add(prefabInstance.gameObject);
    }

    public void AddZombieCharacter(int x, int y)
    {
        prefabInstance = Instantiate(zombieCharacterPrefab, new Vector3(0, 0), Quaternion.identity) as Transform;
        prefabInstance.parent = transform;

        zombieCharacter = prefabInstance.gameObject.GetComponent<OldZombieCharacter>();

        zombieCharacter.Move(x, y);

        zombieCharacters.Add(prefabInstance.gameObject);
    }

    public void RemoveHumanCharacter(GameObject human)
    {
        cityInstance.SetPassable((int)human.transform.position.x, (int)human.transform.position.y, true);
        humanCharacters.Remove(human);
        Destroy(human);
    }

    public void RemoveZombieCharacter(GameObject zombie)
    {
        cityInstance.SetPassable((int)zombie.transform.position.x, (int)zombie.transform.position.y, true);
        zombieCharacters.Remove(zombie);
        Destroy(zombie);
    }

    public bool IsPassable(int x, int y)
    {
        return cityInstance.IsPassable(x, y);
    }

    public void SetPassable(int x, int y, bool passable)
    {
        cityInstance.SetPassable(x, y, passable);
    }

}
