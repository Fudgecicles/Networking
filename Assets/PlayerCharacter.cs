using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;

public class PlayerCharacter : NetworkBehaviour {

    public float movementForce;
    Rigidbody body;

	// Use this for initialization
	void Start () {
        body = GetComponent<Rigidbody>();
	}
	
	// Update is called once per frame
	void Update () {
        if (Input.GetKey(KeyCode.A))
        {
            body.AddForce(Vector3.left * movementForce);
        }
        if (Input.GetKey(KeyCode.W))
        {
            body.AddForce(Vector3.forward * movementForce);

        }
        if (Input.GetKey(KeyCode.D))
        {
            body.AddForce(Vector3.left * -movementForce);

        }
        if (Input.GetKey(KeyCode.S))
        {
            body.AddForce(Vector3.forward * -movementForce);
        }
    }
}
