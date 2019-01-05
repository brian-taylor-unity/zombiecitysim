using UnityEngine;
using System.Collections;

public class OldHumanCharacter : MonoBehaviour
{
	public int x;
	public int y;

	private OldGameController gameController;

	void Awake()
	{
		gameController = GameObject.FindGameObjectWithTag(Tags.gameController).GetComponent<OldGameController>();
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
	
	}

	public void Move(int newX, int newY)
	{
		if (x != 0 && y != 0) {
			gameController.SetPassable(x, y, true);
		}
		
		x = newX;
		y = newY;
		gameController.SetPassable(x, y, false);
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
			gameController.SetPassable(x, y, true);
			gameController.SetPassable(newX, newY, false);
		}

		x = newX;
		y = newY;
		transform.localPosition = new Vector3(newX, newY);
	}
}
