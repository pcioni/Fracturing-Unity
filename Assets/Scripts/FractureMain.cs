using UnityEngine;
using System.Collections;

public class FractureMain : MonoBehaviour {

	private int generation = 1;
	private int maxGenerations = 3;
	private int numCutsToMake = 2;

	private ConvexHull hull;

	public void OnCollisionEnter(Collision c) {
		foreach (ContactPoint cp in c.contacts) {
			if (cp.otherCollider == c.collider)
				Fracture(cp.point);
		}
	}

	/// Fractures a GameObject at a point, instantiating the pieces as new GameObjects.
	/// Create local planes -> create new convex hulls -> create new GameObjects
	public void Fracture(Vector3 point) {
		if (generation < maxGenerations) {
			generation++;

			//fracture our hull using randomly generated planes passing through the contact point
			Plane[] planes = new Plane[numCutsToMake];
			for (int i = 0; i < planes.Length; i++) 
				planes[i] = new Plane(Random.onUnitSphere, point);
			
			SplitPlanes(planes);
		}
	}

	public void SplitPlanes(Plane[] planes) {
		//TODO: Implement this
	}
}
