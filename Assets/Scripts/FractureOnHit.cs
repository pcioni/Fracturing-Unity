using System.Collections.Generic;
using UnityEngine;

public class FractureOnHit : MonoBehaviour {
	public float requiredForce = 1.0f;

	public void OnCollisionEnter(Collision c) {
		if (c.impactForceSum.magnitude >= requiredForce) {
			foreach (ContactPoint cp in c.contacts) {
				if (cp.otherCollider == c.collider) {
					gameObject.GetComponent<FractureMain>().Fracture(transform.InverseTransformPoint (cp.point));
					break;
				}
			}
		}
	}
}