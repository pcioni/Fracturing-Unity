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
 *   http://stackoverflow.com/questions/2924795/fastest-way-to-compute-point-to-triangle-distance-in-3d
 *   http://stackoverflow.com/questions/2924795/fastest-way-to-compute-point-to-triangle-distance-in-3d
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

	public List<int[]> Fill() {
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
			int zero;
			int index = 0;
			int unsuitableTriangles = 0;
			while (loop.Count >= 3) {
				// Evaluate triangle
				if (index == 0)
					zero = loop.Count -1;
				else
					zero = index - 1;
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

					//TODO: also dont use ref vars here too pls
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
					if (IsPointInsideTriangle(point, triangle0, triangle1, triangle2)) 
						return true;
				}
			}
		}
		return false;
	}

	//For each loop, check the three points. make vectors of P1-P0 && P2-P1. Check if this line pair is concave.
	//Store these as a big list of bools of "concave" or "not concave". We don't really care about HOW concave they
	//  are so much as if they're just concave in general.
	private void LocateConcavities() {
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


	//Find the closest point in a triangle, insert a loop at that point, and remove the loop at the previous point.
	//TODO: don't use ref vars pls
	private bool MergeLoops(int first, int second, int third, List<int> loop, List<bool> concavity, out int swallowedLoopIndex) {
		int[] loopInfo = FindClosestPointInTriangle(first, second, third, loop);
		int otherLoopIndex = loopInfo[0];
		int otherLoopLocation = loopInfo[1];
		
		if (otherLoopIndex != -1) {
			//Swallow the other loop
			InsertLoop(first, loop, concavity, otherLoopLocation, loops[otherLoopIndex], concavities[otherLoopIndex]);
		
			//Remove the obsolete loop
			loops.RemoveAt(otherLoopIndex);
			concavities.RemoveAt(otherLoopIndex);
			swallowedLoopIndex = otherLoopIndex;

			return true;
		}

		swallowedLoopIndex = -1;
		return false;
	}

	public static bool IsPointInsideTriangle(Vector3 point, Vector3 triPoint0, Vector3 triPoint1, Vector3 triPoint2) {

		//This if statement checks to see if the point is on the same plane as the triangle.
		if (Vector3.Dot (triPoint2 - triPoint0, Vector3.Cross (triPoint1 - triPoint0, point - triPoint2)) == 0) {

			//If the point is, then it uses the Barycentric coordinates to check whether or not the point is within the 2D triangle.
			Vector3 v0 = triPoint2 - triPoint0;
			Vector3 v1 = triPoint1 - triPoint0;
			Vector3 v2 = point - triPoint0;

			float dot00 = Vector3.Dot (v0, v0);
			float dot01 = Vector3.Dot (v0, v1);
			float dot02 = Vector3.Dot (v0, v2);
			float dot11 = Vector3.Dot (v1, v1);
			float dot12 = Vector3.Dot (v1, v2);

			float invDenom = 1 / (dot00 * dot11 - dot01 * dot01);
			float u = (dot11 * dot02 - dot01 * dot12) * invDenom;
			float v = (dot00 * dot12 - dot01 * dot02) * invDenom;

			return (u >= 0 && v >= 0 && u + v < 1);
		} else {
			return false;
		}
	}

	private int[] FindClosestPointInTriangle(int first, int second, int third, List<int> loop) {
		//TODO: IMPLEMENT ME pls dan
		//What the title says. Given three points of a triangle and their respective loop, find the closest point.

		//this function is only CHECKING to see if we can find a point on the given triangle.
		//I.E., if we can't find a loop, return false.

		int closestLoopIndex = -1;
		int closestLoopLocation = 0;
		float closestDistance = -1f;

		Vector3 triPoint1 = points[edges[loop[first]]];
		Vector3 triPoint2 = points[edges[loop[second]]];
		Vector3 triPoint3 = points[edges[loop[third]]];

		Vector3 triPoint1Normal = Vector3.Cross(normalPlane, triPoint2 - triPoint1);

		//iterate through all existing loops
		for (int x = 0; x < loops.Count; x++) {
			//if it isn't the loop we've been passed
			if (loop != loops [x]) {
				//iterate through all points in the loop
				for (int c = 0; c < loops [x].Count; c++) {
					Vector3 point = points [edges [loops [x] [c]]];
					//check if the point is in the triangle we've been passed
					if (IsPointInsideTriangle (point, triPoint1, triPoint2, triPoint3)) {
						//check if the point is closer than the current closest point
						if (Vector3.SqrMagnitude (point - triPoint1Normal) < closestDistance || closestDistance == -1f) {
							closestLoopIndex = x;
							closestLoopLocation = c;
							closestDistance = -1f;
						}
					}
				}
			}
		}

		int[] result = new int[2];  //0 = loopIndex, 1 = loopLocation
		result[0] = closestLoopIndex;
		result[1] = closestLoopLocation;

		return result;
	}
	private void InsertLoop(int insertLocation, List<int> loop, List<bool> concavity, int otherAnchorLocation, List<int> otherLoop, List<bool> otherConcavity) {
		//Insert a loop into a mesh give the given attributes. 
		//This effectively "closes" the hole in the original mesh.
		int insertPoint = edges[loop[insertLocation]];
		int anchorPoint = edges[otherLoop[otherAnchorLocation]];

		edges.Add(anchorPoint);
		edges.Add(insertPoint);
		edges.Add(insertPoint);
		edges.Add(anchorPoint);

		duplicateEdges.Add(edges.Count); //Save the last added duplicate edge
		int[] insertLoop = new int[otherLoop.Count + 2]; //Insert the other loop into this loop
		bool[] insertConcavity = new bool[otherConcavity.Count + 2];
		int index = 0;
		insertLoop[index] = edges.Count;
		insertConcavity[index++] = false;

		for (int i = otherAnchorLocation; i < otherLoop.Count; i++) {
			insertLoop[index] = otherLoop[i];
			insertConcavity[index++] = otherConcavity[i];
		}
		for (int i = 0; i < otherAnchorLocation; i++) {
			insertLoop[index] = otherLoop[i];
			insertConcavity[index++] = otherConcavity[i];
		}

		insertLoop[index] = edges.Count;
		insertConcavity[index] = false;
		loop.InsertRange(insertLocation, insertLoop);
		concavity.InsertRange(insertLocation, insertConcavity);

		// Update concavity
		int previousLocation;
		if (insertLocation == 0) 
			previousLocation = loop.Count - 1;
		else
			previousLocation = insertLocation - 1;
		
		UpdateConcavity(previousLocation, loop, concavity);
		UpdateConcavity(insertLocation, loop, concavity);
		UpdateConcavity(insertLocation + otherLoop.Count, loop, concavity);
		UpdateConcavity(insertLocation + otherLoop.Count + 1, loop, concavity);
	}

	private void UpdateConcavity(int index, List<int> loop, List<bool> concavity) {
		int firstEdge = loop[index];
		int secondEdge = loop[(index + 1) % loop.Count];

		Vector3 firstLine = points[edges[firstEdge + 1]] - points[edges[firstEdge]];
		Vector3 secondLine = points[edges[secondEdge + 1]] - points[edges[secondEdge]];

		concavity[index] = CheckLinePairConcavity(firstLine, secondLine);
	}

	private void FillTriangle(int zero, int first, int second, int third, List<int> loop, List<bool> concavity) {
		//create a new triangle out of 3 points. Create the 3 new tri edges.
		//Update the concavity using a "zero-line", "cross-line", and "third-line".
		//check the concavity of the ZeroCross and CrossThird pairs.

		int firstPoint = edges[loop[first]];
		int secondPoint = edges[loop[second]];
		int thirdPoint = edges[loop[third]];

		int crossEdge;

		if (loop.Count != 3) {
			//Create the cross edge
			crossEdge = edges.Count;
			edges.Add(firstPoint);
			edges.Add(thirdPoint);
		}
		else
			crossEdge = loop[third]; //Use the third edge as the cross edge

		// Add new triangle
		triangles.Add(firstPoint);
		triangles.Add(secondPoint);
		triangles.Add(thirdPoint);
		triangleEdges.Add(loop[first]);
		triangleEdges.Add(loop[second]);
		triangleEdges.Add(crossEdge);

		// Update loop
		loop[second] = crossEdge;
		loop.RemoveAt(first);

		// Update concavity; always update in order to support zero-length edges
		Vector3 zeroLine = points[firstPoint] - points[edges[loop[zero]]];
		Vector3 crossLine = points[thirdPoint] - points[firstPoint];
		Vector3 thirdLine = points[edges[loop[third] + 1]] - points[thirdPoint];

		concavity[zero] = CheckLinePairConcavity(zeroLine, crossLine);

		concavity[second] = CheckLinePairConcavity(crossLine, thirdLine);
		concavity.RemoveAt(first);
	}

	private bool LocateLoops() {
		//TODO: IMPLEMENT ME
		//for each edge, take edge[i*2] ** edge[i*2+1]. Check if the current edge connects with prev edge.
		//if it does, add it to the loop. if th ecurrent edge ends the loop, add the complete loop to loops
		//   and clear loop.
		loops = new List<List<int>>();
		List<int> loop = new List<int>(edges.Count / 2);

		for (int i = 0; i < edges.Count / 2; i *= 2) {
			int edge = i * 2;
			int endPoint = edges[edge + 1];
			loop.Add(edge);
			//Check if edge ends the loop
			if (endPoint == edges[loop[0]]) {
				loops.Add(loop);
				loop = new List<int>();
			}
		}
	}

	//Thanks math textbook
	private bool CheckLinePairConcavity(Vector3 line0, Vector3 line1) {
		//TODO: DAAAAN FIX ME
		//Fixed ;D
		return (Mathf.Acos((Vector3.Dot(line0, line1)) / (Vector3.Magnitude(line0) * Vector3.Magnitude(line1))) > 0.0f);
	}

}

/* alternate old method for IPIT
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
*/
// LPC //return Vector3.Dot(line1, Vector3.Cross(line0, normalPlane) ) > 0.0f;

