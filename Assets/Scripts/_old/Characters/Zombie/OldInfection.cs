using UnityEngine;
using System.Collections;

public class OldInfection : MonoBehaviour 
{
	// Use this for initialization
	void Start ()
	{

	}
	
	// Update is called once per frame
	void Update () 
	{
	
	}

	void OnTriggerEnter2D(Collider2D other)
	{
		int x, y;
		
		// Check if human
		if (other.gameObject.tag == Tags.human)
		{
			// Change into zombie
			x = (int)other.transform.position.x;
			y = (int)other.transform.position.y;
			
			// Remove human gameObject
			OldGameController.instance.RemoveHumanCharacter(other.gameObject);
			
			// Instatiate new zombie gameObject in its place
			OldGameController.instance.AddZombieCharacter(x, y);
		}
	}
}
