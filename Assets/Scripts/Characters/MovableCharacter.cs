using UnityEngine;
using System.Collections;

public abstract class MovableCharacter : MonoBehaviour
{
	public float moveTime = 0.1f;
	public LayerMask blockingLayer;

	private BoxCollider2D boxCollider;
	private Rigidbody2D rb2D;
	private float inverseMoveTime;
	private GameController gameController;

	protected virtual void Start()
	{
		boxCollider = GetComponent<BoxCollider2D>();
		rb2D = GetComponent<Rigidbody2D>();
		inverseMoveTime = 1f / moveTime;
	}

    // Check to see if movement is available towards xDir, yDir
    // return true if able
    //  false if unable (movement is blocked)
	protected bool Move(int xDir, int yDir, out RaycastHit2D hit)
	{
		Vector2 start = transform.position;

		Vector2 end = start + new Vector2(xDir, yDir);

		boxCollider.enabled = false;

		hit = Physics2D.Linecast(start, end, blockingLayer);

		boxCollider.enabled = true;

		if (hit.transform == null)
        {
            Debug.Log("smoothmove");
			StartCoroutine(SmoothMovement(end));

            return true;
		}

		return false;
	}

	protected IEnumerator SmoothMovement(Vector3 end)
	{
		float sqrRemainingDistance = (transform.position - end).sqrMagnitude;

		while (sqrRemainingDistance > float.Epsilon) {
			Vector3 newPosition = Vector3.MoveTowards(rb2D.position, end, inverseMoveTime * Time.deltaTime);

			rb2D.MovePosition(newPosition);

			sqrRemainingDistance = (transform.position - end).sqrMagnitude;

			yield return null;
		}
	}

	protected virtual bool AttemptMove<T>(int xDir, int yDir)
		where T : Component
	{
		RaycastHit2D hit;

		bool canMove = Move(xDir, yDir, out hit);

		if (hit.transform == null)
			return true;

		T hitComponent = hit.transform.GetComponent<T>();

        if (!canMove && hitComponent != null)
        {
            OnCantMove(hitComponent);
            
            // return true to indicate a "move" was taken
            return true;
        }

        return false;
	}

	protected abstract void OnCantMove<T>(T component)
		where T : Component;
}
