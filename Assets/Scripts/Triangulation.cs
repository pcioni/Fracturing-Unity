using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/*
 * A triangulation class based on Ear-Clipping methods.
 * Fills concave polygons with holes and respective geometry.
 * 
 * Based on the following sources:
 *   http://www.geometrictools.com/Documentation/TriangulationByEarClipping.pdf
 *   http://cecchi.me/js/point-picking.js.src
 *   http://digitalscholarship.unlv.edu/cgi/viewcontent.cgi?article=2314&context=thesesdissertations
 *   https://arxiv.org/ftp/arxiv/papers/1212/1212.6038.pdf
 *   https://github.com/libgdx/libgdx/blob/master/gdx/src/com/badlogic/gdx/math/EarClippingTriangulator.java
 */
public class Triangulation : MonoBehaviour {

	private List<Vector3> points;
	private List<int> edges;
	private List<int> triangles;
	private List<int> triangleEdges;

	private List<List<int>> loops;
	private List<List<bool>> concavities; //some qHull black magic
	private List<int> duplicateEdges;

	private Vector3 normalPlane;
	private int initEdgeCount;

	//CONSTRUCTOR
	public Triangulation(List<Vector3> points, List<int> edges, Vector3 normalPlane) {
		this.points = new List<Vector3>(points);
		this.edges = new List<int>(edges);
		this.triangles = new List<int>();
		this.triangleEdges = new List<int>();

		this.normalPlane = normalPlane;
		this.initEdgeCount = edges.Count;
	}

	public List<int[]>  Fill() {
		List<int[]> edgesTrisTriedges = new List<int[]>(3);

		edgesTrisTriedges.Add(new int[]{});
		edgesTrisTriedges.Add(new int[]{});
		edgesTrisTriedges.Add(new int[]{});

		// Prepare triangulation
		LocateLoops(); //ehehehe alliteration
		LocateConcavities();
		duplicateEdges = new List<int>();

		// Triangulate loops
		for (int i = 0; i < loops.Count; i++) {
			List<int> loop = loops[i];
			List<bool> concavity = concavities[i];

			// Triangulate loop
			int index = 0;
			int unsuitableTriangles = 0;
			while (loop.Count >= 3) {
				// Evaluate triangle
				int zero = index == 0 ? loop.Count - 1 : index - 1;
				int first = index;
				int second = (index + 1) % loop.Count;
				int third = (index + 2) % loop.Count;

				if (concavity[first] || IsTriangleOverlappingLoop(first, second, third, loop, concavity)) {
					// This triangle is not an ear, examine the next one
					index++;
					unsuitableTriangles++;
				}
				else {
					// Evaluate loop merge
					int swallowedLoopIndex;

					if (MergeLoops(first, second, third, loop, concavity, out swallowedLoopIndex)) {
						if (swallowedLoopIndex < i) 
							i--; // Merge occured, adjust loop index
					}
					else 
						// No merge occured, fill triangle
						FillTriangle(zero, first, second, third, loop, concavity);

					// Suitable triangles may have appeared
					unsuitableTriangles = 0;
				}

				if (unsuitableTriangles >= loop.Count) 
					break; // No more suitable triangles in this loop, continue with the next one

				// Wrap index
				if (index >= loop.Count) {
					index = 0;
					unsuitableTriangles = 0;
				}
			}

			// Is the loop filled?
			if (loop.Count <= 2) {
				// Remove the loop in order to avoid future merges
				loops.RemoveAt(i);
				concavities.RemoveAt(i);
				i--;
			}
		}

		/* TODO: Check if this is necessary. No noticeable difference, 
		 *  	 but smart people on the internet told me to use it.
		// Fill any remaining loops using triangle fans
		for (int i = 0; i < loops.Count; i++) {
			List<int> loop = loops[i];
			List<bool> concavity = concavities[i];

			while (loop.Count >= 3)
				FillTriangle(0, 1, 2, 3 % loop.Count, loop, concavity);
		}
		*/

		// Finish triangulation
		RemoveDuplicateEdges();

		//TODO: This doesn't really need to be it's own function.
		edgesTrisTriedges = SetOutput(edgesTrisTriedges);
		return edgesTrisTriedges;
	}

	private List<int[]> SetOutput(List<int[]> edgesTrisTriedges)
	{
		//Set edges
		int newEdgeCount = edges.Count - initEdgeCount;
		if (newEdgeCount > 0) {
			edgesTrisTriedges[0] = new int[newEdgeCount];
			edges.CopyTo(initEdgeCount, edgesTrisTriedges[0], 0, newEdgeCount);
		}
		else 
			edgesTrisTriedges[0] = new int[0];

		//Set triangles
		edgesTrisTriedges[1] = triangles.ToArray();

		//Set triangle edges
		edgesTrisTriedges[2] = new int[triangleEdges.Count];

		for (int i = 0; i < triangleEdges.Count; i++) 
			edgesTrisTriedges[2][i] = triangleEdges[i] / 2;

		return edgesTrisTriedges;
	}
		
	private void RemoveDuplicateEdges() {
		for (int i = 0; i < duplicateEdges.Count; i++) {
			int edge = duplicateEdges[i];
			edges.RemoveRange(edge, 2); //Remove the duplicate edge

			//Update indices in triangle edges
			//TODO: consider doing this in a non-syntatically-moronic way.
			for (int j = 0, l = i + 1; j < triangleEdges.Count || l < duplicateEdges.Count; j++, i++) {
				if (l < duplicateEdges.Count && duplicateEdges[l] >= edge)
					duplicateEdges[j] -= 2; //Edge is in front of the duplicate edge
	
				if (j < triangleEdges.Count && triangleEdges[j] >= edge) 
					triangleEdges[j] -= 2;  //edge is in front of the duplicate edge
			}

		}
	}

	private bool IsTriangleOverlappingLoop() {
		//TODO: IMPLEMENT ME
		//for each loop, check for concavitiy, and check if the "reflex point" is inside the triangle.
	}
	private bool MergeLoops() {
		//TODO: IMPLEMENT ME
		//find the closest point in a triangle, insert a loop at that point, and remove the loop at the previous point.
	}
	private void FillTriangle() {
		//TODO: IMPLEMENT ME
		//fookin gross eh'
		//create a new triangle out of 3 points. Create the 3 new tri edges.
		//Update the concavity using a "zero-line", "cross-line", and "third-line".
		//check the concavity of the ZeroCross and CrossThird pairs.
	}
	private bool LocateLoops() {
		//TODO: IMPLEMENT ME
		//for each edge, take edge[i*2] ** edge[i*2+1]. Check if the current edges end the loop.
	}
	private bool LocateConcavities() {
		//TODO: IMPLEMENT ME
		//for each loop, check the three points. make vectors of P1-P0 && P2-P1. Check if this line pair is concave.
	}

}
