using UnityEngine;
using System.Collections;

public class ZombieCharacter : MovableCharacter
{
    public int turnsPerMove = 2;

    private int turnsSinceMove = 1;
    private Vision vision;
    private Transform moveTarget;

    void Awake()
    {
        vision = gameObject.GetComponentInChildren<Vision>();
    }

    // Use this for initialization
    protected override void Start()
    {
        moveTarget = transform;

        base.Start();
    }

    // Update is called once per frame
    void Update()
    {

    }

    protected override bool AttemptMove<T>(int xDir, int yDir)
    {
        if (turnsSinceMove != turnsPerMove)
        {
            turnsSinceMove++;
            return false;
        }
        turnsSinceMove = 1;

        return base.AttemptMove<T>(xDir, yDir);
    }

    public void MoveZombie()
    {
        GameObject closestHuman;
        int xDir = 0;
        int yDir = 0;
        float xDistance = 0f;
        float yDistance = 0f;

        // Check if human is within sight
        closestHuman = vision.ClosestHuman();

        if (closestHuman != gameObject)
            moveTarget = closestHuman.transform;

        // Zombie has a current target or a previously seen target
        if (moveTarget != transform)
        {
            // Check whether we need to move vertically or horizontally
            xDistance = Mathf.Abs(moveTarget.position.x - transform.position.x);
            yDistance = Mathf.Abs(moveTarget.position.y - transform.position.y);

            // If zombie is closer vertically than horizontally
            if (yDistance < xDistance)
                yDir = moveTarget.position.y > transform.position.y ? 1 : -1;
            else
                xDir = moveTarget.position.x > transform.position.x ? 1 : -1;

            // Try to move in that direction
            AttemptMove<HumanCharacter>(xDir, yDir);
        }
    }

    protected override void OnCantMove<T>(T component)
    {
        HumanCharacter human = component as HumanCharacter;

        // Infect human
    }
}
