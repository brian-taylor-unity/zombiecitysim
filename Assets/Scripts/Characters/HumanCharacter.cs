using UnityEngine;
using System.Collections;

public class HumanCharacter : MonoBehaviour
{
	public int x;
	public int y;

	private GameController gameController;

	void Awake()
	{
		gameController = GameObject.FindGameObjectWithTag(Tags.gameController).GetComponent<GameController>();
		x = 0;
		y = 0;
	}

	// Use this for initialization
	void Start()
	{

	}
	
	// Update is called once per frame
	void Update()
	{
        Move();
	}

	public void Move(int newX, int newY)
	{
        if (!gameController.IsPassable(newX, newY))
            return;

        x = newX;
		y = newY;
		transform.localPosition = new Vector3(x, y);
	}

	public void Move()
	{
		MoveRandomly();
	}

	public void MoveRandomly()
	{
		bool passable = false;
		int newX, newY, direction;

		newX = x;
		newY = y;

		do {
			if (!gameController.IsPassable(x, y + 1) &&
				!gameController.IsPassable(x + 1, y) &&
				!gameController.IsPassable(x, y - 1) &&
				!gameController.IsPassable(x - 1, y)) {
				// Can't move in any direction, pretend as though you have moved
				passable = true;
			} else {
				direction = Random.Range(0, 4);
				switch (direction) {
				case 0:
					newX = x;
					newY = y + 1;
					if (gameController.IsPassable(newX, newY))
						passable = true;
					break;
				case 1:
					newX = x + 1;
					newY = y;
					if (gameController.IsPassable(newX, newY))
						passable = true;
					break;
				case 2:
					newX = x;
					newY = y - 1;
					if (gameController.IsPassable(newX, newY))
						passable = true;
					break;
				case 3:
					newX = x - 1;
					newY = y;
					if (gameController.IsPassable(newX, newY))
						passable = true;
					break;
				}
			}
		} while (!passable);
		
		if (newX != x || newY != y) {
            x = newX;
            y = newY;
            transform.localPosition = new Vector3(newX, newY);
        }
    }
}
