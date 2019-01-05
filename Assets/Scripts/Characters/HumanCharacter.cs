using UnityEngine;
using System.Collections;

public class HumanCharacter : MovableCharacter
{
	public int turnsPerMove = 1;

	private int turnsSinceMove = 1;

	// Use this for initialization
	protected override void Start()
	{

        base.Start();
	}
	
	// Update is called once per frame
	void Update()
	{
	
	}

	protected override bool AttemptMove<T>(int xDir, int yDir)
	{
        Debug.Log("Human attemptMove");
        return base.AttemptMove<T>(xDir, yDir);
	}

    public void MoveHuman()
    {
        if (turnsSinceMove != turnsPerMove)
        {
            turnsSinceMove++;
            return;
        }
        turnsSinceMove = 1;


        // Move Randomly
        bool didMove = false;
        int moveTries = 0;
        int direction = Random.Range(0, 4);

        while (moveTries < 4)
        {
            switch (direction)
            {
                // Move Up
                case 0:
                    didMove = AttemptMove<ZombieCharacter>(0, 1);
                    if (!didMove)
                        moveTries++;
                    break;
                // Move Right
                case 1:
                    didMove = AttemptMove<ZombieCharacter>(1, 0);
                    if (!didMove)
                        moveTries++;
                    break;
                // Move Down
                case 2:
                    didMove = AttemptMove<ZombieCharacter>(0, -1);
                    if (!didMove)
                        moveTries++;
                    break;
                // Move Left
                case 3:
                    didMove = AttemptMove<ZombieCharacter>(-1, 0);
                    if (!didMove)
                        moveTries++;
                    break;
            }
            if (didMove)
                break;

            direction = Random.Range(0, 4);
        }

    }

	protected override void OnCantMove<T>(T component)
	{
        // Remove zombie health
	}

    public void BecomeZombie()
    {

    }
}
