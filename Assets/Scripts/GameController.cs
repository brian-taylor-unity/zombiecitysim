using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;
using System.Collections.Generic;

public class GameController : MonoBehaviour
{
    public static GameController instance = null;

    public int numTilesX = 130;
    public int numTilesY = 130;
    public int numStreets = 30;
    public int numHumans = 1000;
    public int numZombies = 1;
    public int zombieVisionDistance = 5;
    public int zombieHearingDistance = 10;
    public int zombieStartingHealth = 70;
    public int zombieDamage = 20;

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
