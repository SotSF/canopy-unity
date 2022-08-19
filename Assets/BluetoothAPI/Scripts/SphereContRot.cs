using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SphereContRot : MonoBehaviour {

	// Update is called once per frame
	void Update () {
		transform.Rotate(Vector3.forward * Time.deltaTime * 300);
		transform.position = new Vector3(2.5f*Mathf.Sin(Time.time * 4) , 1, 0);
	}
}
