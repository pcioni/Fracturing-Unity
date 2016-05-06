using System.Collections.Generic;
using UnityEngine;

public class FractureMain : MonoBehaviour {
	public int generation = 1;
	public int maxGenerations = 3;
	public int numCutsToMake = 2;

	private ConvexHull hull;

	public float forceToFracture;
	public bool fillCut = true;

	public void Start() {
		if (hull == null) 
			hull = new ConvexHull(GetComponent<MeshFilter>().mesh);
	}

	///Fractures a GameObject at a point, instantiating the pieces as new GameObjects.
	///Create local planes -> create new convex hulls -> create new GameObjects
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
		if (planes != null && planes.Length > 0 && hull != null) {
			planes = createLocalPlanes(planes);
			List<ConvexHull> newHulls = CreateNewConvexHulls(planes);
			GameObject[] newGameObjects = CreateNewGameObjects(newHulls);
			Destroy(gameObject);
		}
	}

	private Plane[] createLocalPlanes(Plane[] planes) {
		for (int i = 0; i < planes.Length; i++) {
			Vector3 localNormal = transform.InverseTransformDirection(planes[i].normal);
			localNormal.Scale(transform.localScale);
			localNormal.Normalize();
			planes[i].normal = localNormal;
		}
		return planes;
	}

	private List<ConvexHull> CreateNewConvexHulls(Plane[] localPlanes) {
		List<ConvexHull> newConvexHulls = new List<ConvexHull>(); 
		newConvexHulls.Add(hull);
		foreach (Plane plane in localPlanes) {
			int previousHullCount = newConvexHulls.Count;

			for (int i = 0; i < previousHullCount; i++) {
				ConvexHull previousHull = newConvexHulls[0];

				//Split the previous hull
				ConvexHull[] newHulls = previousHull.Split(plane.normal * -plane.distance, plane.normal, fillCut);
				ConvexHull a = newHulls[0];
				ConvexHull b = newHulls[1];

				newConvexHulls.Remove(previousHull);
				newConvexHulls.Add(a);
				newConvexHulls.Add(b);
			}
		}
		return newConvexHulls;
	}

	private GameObject[] CreateNewGameObjects(List<ConvexHull> newHulls) {
		//TODO: Implement this
		GameObject[] newGameObjects = new GameObject[newHulls.Count];

		//Get new meshes
		Mesh[] newMeshes = new Mesh[newHulls.Count];
		float[] newVolumes = new float[newHulls.Count];

		float totalVolume = 0.0f;

		for (int i = 0; i < newHulls.Count; i++) {
			Mesh mesh = newHulls[i].GetMesh();
			Vector3 size = mesh.bounds.size;
			float volume = size.x * size.y * size.z;

			newMeshes[i] = mesh;
			newVolumes[i] = volume;

			totalVolume += volume;
		}

		for (int i = 0; i < newHulls.Count; i++) {
			ConvexHull newHull = newHulls[i];
			Mesh newMesh = newMeshes[i];
			float volume = newVolumes[i];

			GameObject newGameObject = (GameObject)Instantiate(gameObject);

			newGameObject.GetComponent<FractureMain>().hull = newHull;
			newGameObject.GetComponent<MeshFilter>().mesh = newMesh;
			newGameObject.GetComponent<MeshCollider>().sharedMesh = newMesh;

			//Set rigidbody
			if (fillCut) {
				Rigidbody newRigidbody = newGameObject.GetComponent<Rigidbody>();
				newRigidbody.mass = GetComponent<Rigidbody>().mass * (volume / totalVolume);
				newRigidbody.velocity = GetComponent<Rigidbody>().GetPointVelocity(newRigidbody.worldCenterOfMass);
				newRigidbody.angularVelocity = GetComponent<Rigidbody>().angularVelocity;
			}

			newGameObjects[i] = newGameObject;
		}
		return newGameObjects;
	}
}