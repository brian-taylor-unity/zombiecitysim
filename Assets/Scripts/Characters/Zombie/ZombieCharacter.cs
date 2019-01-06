using UnityEngine;
using System.Collections;

public class ZombieCharacter : MonoBehaviour
{
    public int x;
    public int y;

    private GameController gameController;
    private Vision vision;

    void Awake()
    {
        gameController = GameObject.FindGameObjectWithTag(Tags.gameController).GetComponent<GameController>();
        vision = gameObject.GetComponentInChildren<Vision>();

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

    public void Move()
    {
        GameObject closestHuman;

        // Check if human is within sight
        closestHuman = vision.ClosestHuman();

        if (closestHuman != gameObject)
        {
            //  move in that direction
            MoveTowards(closestHuman.transform);
        }

        // Move in the previous direction

        // If didn't move last turn,
        //  move randomly
        else
        {
            MoveRandomly();
        }
    }

    void MoveTowards(Transform target)
    {
        bool preferHorizontal;

        preferHorizontal = Mathf.Abs(x - target.position.x) > Mathf.Abs(y - target.position.y);

        // We need to move to the right more than vertically
        if (preferHorizontal)
        {
            // We need to move to the right
            if (x < target.position.x)
            {
                if (gameController.IsPassable(x + 1, y))
                {
                    Move(x + 1, y);
                    return;
                }
            }
            // We need to move to the left
            if (x > target.position.x)
            {
                if (gameController.IsPassable(x - 1, y))
                {
                    Move(x - 1, y);
                    return;
                }
            }
        }

        // We either need to move vertically
        // or needed to move horizontally but couldn't
        if (y < target.position.y)
        {
            // Try to move up
            if (gameController.IsPassable(x, y + 1))
            {
                Move(x, y + 1);
                return;
            }
        }

        // We haven't moved and
        // we need to move down
        if (y > target.position.y)
        {
            // Try to move down
            if (gameController.IsPassable(x, y - 1))
            {
                Move(x, y - 1);
                return;
            }
        }

        // We didn't move at all
    }

    public void Move(int newX, int newY)
    {
        if (!gameController.IsPassable(newX, newY))
            return;

        x = newX;
        y = newY;
        transform.localPosition = new Vector3(x, y);
    }

    public void MoveRandomly()
    {
        bool passable = false;
        int newX, newY, direction;

        newX = x;
        newY = y;

        do
        {
            if (!gameController.IsPassable(x, y + 1) &&
                !gameController.IsPassable(x + 1, y) &&
                !gameController.IsPassable(x, y - 1) &&
                !gameController.IsPassable(x - 1, y))
            {
                // Can't move in any direction, pretend as though you have moved
                passable = true;
            }
            else
            {
                direction = Random.Range(0, 4);
                switch (direction)
                {
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

        if (newX != x || newY != y)
        {
            x = newX;
            y = newY;
            transform.localPosition = new Vector3(newX, newY);
        }
    }
}
