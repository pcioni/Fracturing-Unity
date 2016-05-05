using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FractureMain : MonoBehaviour {

	private int generation = 1;
	private int maxGenerations = 3;
	private int numCutsToMake = 2;

	private ConvexHull hull;

	public void Start() {
		if (hull == null)
			hull = new ConvexHull(GetComponent<MeshFilter>().mesh);
	}

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
		if (planes != null && planes.Length > 0 && hull != null && !hull.IsEmpty()) {
			Plane[] localPlanes = CreateLocalPlanes(planes);
			List<ConvexHull> newHulls = CreateNewConvexHulls(localPlanes);
			GameObject[] newGameObjects = CreateNewGameObjects(newHulls);
			Destroy(gameObject);
		}
	}

	private Plane[] CreateLocalPlanes(Plane[] planes) {
		//TODO: Implement this
		Plane[] newLocalPlanes = new Plane[planes.Length];
		return newLocalPlanes;
	}

	private List<ConvexHull> CreateNewConvexHulls(Plane[] localPlanes) {
		//TODO: Implement this
		List<ConvexHull> newConvexHulls = new List<ConvexHull>(); 
		return newConvexHulls;
	}

	private GameObject[] CreateNewGameObjects(List<ConvexHull> newHulls) {
		//TODO: Implement this
		GameObject[] newGameObjects = new GameObject[newHulls.Count];
		return newGameObjects;
	}


}
