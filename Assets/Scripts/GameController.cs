using UnityEngine;

public class GameController : MonoBehaviour
{
    public static GameController instance = null;

    public int numTilesX = 130;
    public int numTilesY = 130;
    public int numStreets = 30;
    public int numHumans = 1000;
    public int numZombies = 1;
    public int zombieVisionDistance = 10;
    public float turnDelayTime = 1.0f;

    void Awake()
    {
        if (instance == null)
            instance = this;
    }

    // Use this for initialization
    void Start()
    {
    }

    // Update is called once per frame
    void Update()
    {
    }
}
