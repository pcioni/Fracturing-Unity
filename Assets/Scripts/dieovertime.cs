using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class dieovertime : MonoBehaviour {
	public GameObject prefabToInstantiate;

	public float speed = 7.0f;

	private float time;
	private float dest;

	public void Start() {
		time = 0.0f;
		dest = 1.5f;
	}

	public void Update() {

		time += Time.deltaTime;
		if (time >= 2) 
			Destroy(gameObject);

	}


}