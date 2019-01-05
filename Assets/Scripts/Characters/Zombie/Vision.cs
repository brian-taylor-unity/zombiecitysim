using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class Vision : MonoBehaviour
{
    public float visionRange = 7.5f;

    private CircleCollider2D circleCollider;
    private List<GameObject> humansInRange;

    // Use this for initialization
    void Awake()
    {
        circleCollider = GetComponent<CircleCollider2D>();
        circleCollider.radius = visionRange;

        humansInRange = new List<GameObject>();
    }

    // Update is called once per frame
    void Update()
    {


    }

    public GameObject ClosestHuman()
    {
        // Find closest of list of humans in range
        float minDistance = visionRange;
        float currDistance;

        GameObject closestHuman = transform.parent.gameObject;

        if (humansInRange.Count == 0)
        {
            return closestHuman;
        }

        foreach (GameObject human in humansInRange)
        {
            if (human != null)
            {
                /*
                LayerMask layerMask = LayerMask.GetMask("Passable");
                RaycastHit2D hit = Physics2D.Raycast(transform.position, human.transform.position, visionRange, ~layerMask);
                Debug.Log(hit.collider.tag);
                Debug.Log (hit.collider.gameObject.name);
                Debug.Log(hit.collider.transform.position);
                if (hit.collider.tag == Tags.human)
                {
                */
                currDistance = Vector3.Distance(transform.position, human.transform.position);
                if (currDistance <= minDistance)
                {
                    minDistance = currDistance;
                    closestHuman = human;
                }
                /*
                }
                */
            }
        }

        return closestHuman;
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        // Add to list of humans in range
        if (other.gameObject.tag == Tags.human)
        {
            humansInRange.Add(other.gameObject);
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        // Remove from list of humans in range
        if (other.gameObject.tag == Tags.human)
        {
            humansInRange.Remove(other.gameObject);
        }
    }
}
