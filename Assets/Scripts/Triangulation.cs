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

		//Prepare triangulation
		LocateLoops(); //ehehehe alliteration
		LocateConcavities();
		duplicateEdges = new List<int>();

		//Triangulate loops
		for (int i = 0; i < loops.Count; i++) {
			List<int> loop = loops[i];
			List<bool> concavity = concavities[i];

			//Triangulate loop
			int index = 0;
			int unsuitableTriangles = 0;
			while (loop.Count >= 3) {
				// Evaluate triangle
				int zero = index == 0 ? loop.Count - 1 : index - 1;
				int first = index;
				int second = (index + 1) % loop.Count;
				int third = (index + 2) % loop.Count;

				if (concavity[first] || IsTriangleOverlappingLoop(edges[loop[first]], edges[loop[second]], edges[loop[third]], loop, concavity)) {
					//Triangle is not an ear
					index++;
					unsuitableTriangles++;
				}
				else {
					//Evaluate loop merge
					int swallowedLoopIndex;

					if (MergeLoops(first, second, third, loop, concavity, out swallowedLoopIndex)) {
						if (swallowedLoopIndex < i) 
							i--; // Merge occured; adjust loop index
					}
					else 
						//No merge occured; fill triangle
						FillTriangle(zero, first, second, third, loop, concavity);

					//Suitable triangles may have appeared
					unsuitableTriangles = 0;
				}

				if (unsuitableTriangles >= loop.Count) 
					break; //No suitable triangles in loop

				//Wrap index
				if (index >= loop.Count) {
					index = 0;
					unsuitableTriangles = 0;
				}
			}

			//Check if loop is complete.
			if (loop.Count <= 2) {
				//Remove the loop to avoid future merges
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

	public static bool IsPointInsideTriangle(Vector3 point, Vector3 triangle0, Vector3 triangle1, Vector3 triangle2) {
		Vector3 triangleNormal = Vector3.Cross(triangle1 - triangle0, triangle2 - triangle0);

		//Discard size zero triangles
		if (Vector3.Cross(triangle1 - triangle0, triangle2 - triangle0) == Vector3.zero)
			return false;

		Vector3 pointTo0 = triangle0 - point;
		Vector3 pointTo1 = triangle1 - point;
		Vector3 pointTo2 = triangle2 - point;

		if (   Vector3.Dot(Vector3.Cross(pointTo0, pointTo1), triangleNormal) < 0.0f
			|| Vector3.Dot(Vector3.Cross(pointTo1, pointTo2), triangleNormal) < 0.0f
			|| Vector3.Dot(Vector3.Cross(pointTo2, pointTo0), triangleNormal) < 0.0f  )
		{ return false; }

		return true;
	}

	//for each loop, check for concavitiy, and check if the "reflex point" is inside the triangle.
	//A lovely gift from github.
	private bool IsTriangleOverlappingLoop(int point0, int point1, int point2, List<int> loop, List<bool> concavity) {
		Vector3 triangle0 = points[point0];
		Vector3 triangle1 = points[point1];
		Vector3 triangle2 = points[point2];

		for (int i = 0; i < loop.Count; i++) {
			if (concavity[i]) {
				int reflexAngleVert = edges[loop[i] + 1];
				//Skip points that aren't in the triangle
				if (reflexAngleVert != point0 && reflexAngleVert != point1 && reflexAngleVert != point2) {
					Vector3 point = points[reflexAngleVert];
					//Check if reflex vertex is inside the triangle
					if (IsPointInsideTriangle(point, triangle0, triangle1, triangle2, normalPlane)) 
						return true;
				}
			}
		}
		return false;
	}

	//Thanks math textbook
	private bool CheckLinePairConcavity(Vector3 line0, Vector3 line1) {
		return Vector3.Dot(line1, Vector3.Cross(line0, normalPlane) ) > 0.0f;
	}

	//For each loop, check the three points. make vectors of P1-P0 && P2-P1. Check if this line pair is concave.
	//Store these as a big list of bools of "concave" or "not concave". We don't really care about HOW concave they
	//  are so much as if they're just concave in general.
	private bool LocateConcavities() {
		concavities = new List<List<bool>>();

		foreach (List<int> loop in loops) {
			List<bool> concavity = new List<bool>(loop.Count);

			for (int i = 0; i < loop.Count; i++) {
				int point0 = edges[loop[i] ];
				int point1 = edges[loop[(i + 1) % loop.Count] ];
				int point2 = edges[loop[(i + 2) % loop.Count] ];

				Vector3 firstLine = points[point1] - points[point0];
				Vector3 secondLine = points[point2] - points[point1];

				concavity.Add(CheckLinePairConcavity(firstLine, secondLine));
			}
			concavities.Add(concavity);
		}
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

}
