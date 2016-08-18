using UnityEngine;
using System.Collections;

public class MatchOrientation : MonoBehaviour {
    public Transform Target;

	// Use this for initialization
	void Start () {
	
	}
	
	// Update is called once per frame
	void Update () {
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null) {
            rb.MoveRotation(Target.transform.rotation);
        } else {
            transform.rotation = Target.rotation;
        }
	}
}
