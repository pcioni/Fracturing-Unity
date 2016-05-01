using UnityEngine;
using System.Collections;

public class FractureOnHit : MonoBehaviour {

	public float fractureThreshold = 0.0f;

	public void OnCollisionEnter(Collision c) {
		if (c.relativeVelocity.magnitude >= fractureThreshold) {
			foreach (ContactPoint cp in c.contacts) {
				gameObject.GetComponent<FractureMain>().Fracture(cp.point);
			}
		}	
	}
}
