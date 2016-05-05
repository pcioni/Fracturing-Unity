using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;


//Creates a convex hull out of geometry data.
/*
 * http://answers.unity3d.com/questions/380233/generating-a-convex-hull.html
 * https://miconvexhull.codeplex.com/
 * http://answers.unity3d.com/questions/983425/creating-a-convex-mesh-not-collider.html
 * https://www.reddit.com/r/Unity3D/comments/3q9qv9/spherical_convex_hull_for_my_planet_generator/?
 * http://www.gdcvault.com/play/1020141/Physics-for-Game-Programmers
 * http://www.qhull.org/
 * http://diskhkme.blogspot.com/2015/11/convex-hull-algorithm-in-unity-5.html
 * https://github.com/abrarjahin/QuickHull2D_unity_js
 */
public class ConvexHull : MonoBehaviour {

	private List<Vector2> uvs; //I don't actually know if we'll implement this
	private List<Vector3> vertices;
	private List<Vector3> normals;
	private List<Vector4> tangents;

	private List<Point> vertexPoints;

	private List<Point> points;
	private List<Edge> edges;
	private List<Triangle> triangles;

	//CONSTRUCTOR
	public ConvexHull(ConvexHull reference) {
		int vertexCount = reference.vertices.Count * 2;

		vertices = new List<Vector3>(vertexCount);
		normals = new List<Vector3>(vertexCount);
		tangents = new List<Vector4>(vertexCount);
		uvs = new List<Vector2>(vertexCount);

		vertexPoints = new List<Point>(vertexCount);

		points = new List<Point>(reference.points.Count * 2);
		edges = new List<Edge>(reference.edges.Count * 2);
		triangles = new List<Triangle>(reference.triangles.Count * 2);
	}

	public ConvexHull(Mesh mesh) {
		vertices = new List<Vector3>(mesh.vertices);
		normals = new List<Vector3>(mesh.normals);
		tangents = new List<Vector4>(mesh.tangents);
		uvs = new List<Vector2>(mesh.uv);

		vertexPoints = new List<Point>(vertices.Count);

		points = new List<Point>();
		edges = new List<Edge>();
		triangles = new List<Triangle>();

		//create vertex-point map
		for (int i = 0; i < vertices.Count; i++) {
			Point p = new Point(vertices[i]);
			Point pointAlreadyExists = points.Find(pExists => pExists.position == p.position);

			//Duplicate points = same vertex. Don't draw the same mesh twice.
			if (pointAlreadyExists == null) 
				vertexPoints.Add(p);
			else 
				vertexPoints.Add(pointAlreadyExists);	
			
			points.Add(p);
		}

		//create edges and triangles
		int[] indices = mesh.triangles;
		for (int i = 0; i < indices.Length / 3; i ++) {
			int triangle = i * 3;
			AddTriangle(indices[triangle + 0], indices[triangle + 1], indices[triangle + 2]);
		}
	}

	private void AddTriangle(int vertex0, int vertex1, int vertex2) {
		Point point0 = vertexPoints[vertex0];
		Point point1 = vertexPoints[vertex1];
		Point point2 = vertexPoints[vertex2];

		Edge edge0 = AddUniqueEdge(point0, point1);
		Edge edge1 = AddUniqueEdge(point1, point2);
		Edge edge2 = AddUniqueEdge(point2, point0);

		triangles.Add(new Triangle(vertex0, vertex1, vertex2, point0, point1, point2, edge0, edge1, edge2));
	}

	private Edge AddUniqueEdge(Point point0, Point point1) {
		Edge e;
		//Duplicate edges = same triangle. Don't draw the same triangle twice.
		Edge edgeAlreadyExists = edges.Find(eExists => (eExists.point0 == point0 && eExists.point1 == point1)
												    || (eExists.point0 == point1 && eExists.point1 == point0));

		if (edgeAlreadyExists == null) {
			e = new Edge(point0, point1);
			edges.Add(e);
			return e;
		}
		else {
			edges.Add(edgeAlreadyExists);
			return edgeAlreadyExists;
		}
	}

	public bool IsEmpty() {
		return points.Count < 4 || edges.Count < 6 || triangles.Count < 4;
	}

	private void AddTriangle(int vertex0, int vertex1, int vertex2) {
		Point point0 = vertexPoints[vertex0];
		Point point1 = vertexPoints[vertex1];
		Point point2 = vertexPoints[vertex2];

		Edge edge0 = AddUniqueEdge(point0, point1);
		Edge edge1 = AddUniqueEdge(point1, point2);
		Edge edge2 = AddUniqueEdge(point2, point0);

		Triangle triangle = new Triangle(vertex0, vertex1, vertex2, point0, point1, point2, edge0, edge1, edge2);

		triangles.Add(triangle);
	}

	private int AddVertex(Vector3 vertex, Vector3 normal, Vector4 tangent, Vector2 uv, Point point) {
		vertices.Add(vertex);
		normals.Add(normal);
		tangents.Add(tangent);
		uvs.Add(uv);
		vertexPoints.Add(point);
		return vertices.Count;
	}

	//TODO: might not need this
	public void Clear() {
		vertices.Clear();
		normals.Clear();
		tangents.Clear();
		uvs.Clear();

		vertexPoints.Clear();

		points.Clear();
		edges.Clear();
		triangles.Clear();
	}

	public Mesh GetMesh() {
		if (!IsEmpty) { //TODO: might not need this if
			// Create vertex array
			Vector3[] vertices = new Vector3[this.vertices.Count];
			Vector3[] normals = new Vector3[this.normals.Count];
			Vector4[] tangents = new Vector4[this.tangents.Count];
			Vector2[] uvs = new Vector2[this.uvs.Count];

			this.vertices.CopyTo(vertices, 0);
			this.normals.CopyTo(normals, 0);
			this.tangents.CopyTo(tangents, 0);
			this.uvs.CopyTo(uvs, 0);

			// Create index array
			int[] indices = new int[triangles.Count * 3];

			int count = 0;

			foreach (Triangle triangle in triangles) {
				indices[count++] = triangle.vertex0;
				indices[count++] = triangle.vertex1;
				indices[count++] = triangle.vertex2;
			}

			// Create output mesh
			Mesh mesh = new Mesh();

			mesh.vertices = vertices;
			mesh.normals = normals;
			mesh.tangents = tangents;
			mesh.uv = uvs;
			mesh.triangles = indices;

			return mesh;
		}
		return null; //TODO: or this
	}

	public ConvexHull[] Split(Vector3 localPointOnPlane, Vector3 localPlaneNormal, bool fillCut) {
		if (localPlaneNormal == Vector3.zero) 
			localPlaneNormal = Vector3.up;

		ConvexHull[] result = new ConvexHull[2];
		ConvexHull a = new ConvexHull(this);
		ConvexHull b = new ConvexHull(this);

		//set indecies of edge array
		int pointCount, edgeCount = 0;
		foreach (Point point in points)
			point.index = pointCount++;
		foreach (Edge edge in edges)
			edge.index = edgeCount++;

		bool[] pointAbovePlane = AssignPoints(a, b, localPointOnPlane, localPlaneNormal);

		int[] oldToNewVertex = AssignVertices(a, b, pointAbovePlane);

		bool[] edgeIntersectsPlane;
		EdgeHit[] edgeHits;
		//"out" parameter to keep this piece-a-junk from pervading it's sticky fingers throughout the entire program
		AssignEdges(a, b, pointAbovePlane, localPointOnPlane, localPlaneNormal, out edgeIntersectsPlane, out edgeHits);

		List<Edge>[] newTris = AssignTriangles(a, b, pointAbovePlane, edgeIntersectsPlane, edgeHits, oldToNewVertex);
		List<Edge> cutEdgesA = newTris[0];
		List<Edge> cutEdgesB = newTris[1];

		if (fillCut) {
			SortCutEdges(cutEdgesA, cutEdgesB);
			FillCutEdges(a, b, cutEdgesA, cutEdgesB, localPlaneNormal);
		}

		//TODO: might not need this
		//ValidateOutput(a, b, localPlaneNormal);

		//TODO: or this
		Clear();

		result[0] = a;
		result[1] = b;
		return result;
	}

	private bool[] AssignPoints(ConvexHull a, ConvexHull b, Vector3 pointOnPlane, Vector3 planeNormal) {
		bool[] pointAbovePlane = new bool[points.Count];
		foreach (Point point in points) {
			bool abovePlane = Vector3.Dot(point.position - pointOnPlane, planeNormal) >= 0.0f;
			pointAbovePlane[point.index] = abovePlane;
			if (abovePlane)
				a.points.Add(point);
			else
				b.points.Add(point);
		}
		return pointAbovePlane;
	}

	private int[] AssignVertices(ConvexHull a, ConvexHull b, bool[] pointAbovePlane) {
		int[] oldToNewVertex = new int[vertices.Count];
		for (int i = 0; i < vertices.Count; i++) {
			Point correspondingPoint = vertexPoints[i];
			if (pointAbovePlane[correspondingPoint.index])
				oldToNewVertex[i] = a.AddVertex(vertices[i], normals[i], tangents[i], uvs[i], correspondingPoint);
			else
				oldToNewVertex[i]=  b.AddVertex(vertices[i], normals[i], tangents[i], uvs[i], correspondingPoint);
		}
		return oldToNewVertex;
	}

	private void AssignEdges(ConvexHull a, ConvexHull b, bool[] pointAbovePlane, Vector3 pointOnPlane, Vector3 planeNormal, out bool[] edgeIntersectsPlane, out EdgeHit[] edgeHits) {
		edgeIntersectsPlane = new bool[edges.Count];
		edgeHits = new EdgeHit[edges.Count];

		foreach (Edge edge in edges) {
			bool abovePlane0 = pointAbovePlane[edge.point0.index];
			bool abovePlane1 = pointAbovePlane[edge.point1.index];

			if (abovePlane0 && abovePlane1)
				a.edges.Add(edge);
			else if (!abovePlane0 && !abovePlane1)
				b.edges.Add(edge);
			else {
				//Split edge
				float denominator = Vector3.Dot(edge.line, planeNormal);
				float scalar = Mathf.Clamp01(Vector3.Dot(pointOnPlane - edge.point0.position, planeNormal) / denominator);
				Vector3 intersection = edge.point0.position + edge.line * scalar;

				//Create new points
				Point pointA = new Point(intersection);
				Point pointB = new Point(intersection);
				a.points.Add(pointA);
				b.points.Add(pointB);

				//Create new edges
				Edge splitA, splitB;
				if (pointAbovePlane[edge.point0.index]) {
					splitA = new Edge(pointA, edge.point0);
					splitB = new Edge(pointB, edge.point1);
				}
				else {
					splitA = new Edge(pointA, edge.point1);
					splitB = new Edge(pointB, edge.point0);
				}
				a.edges.Add(splitA);
				b.edges.Add(splitB);

				//Set flags
				edgeIntersectsPlane[edge.index] = true;
				edgeHits[edge.index] = new EdgeHit();
				edgeHits[edge.index].scalar = scalar;
				edgeHits[edge.index].splitA = splitA;
				edgeHits[edge.index].splitB = splitB;
			}
		}
	}

	private List<Edge>[] AssignTriangles(ConvexHull a, ConvexHull b, bool[] pointAbovePlane, bool[] edgeIntersectsPlane, EdgeHit[] edgeHits, int[] oldToNewVertex) {
		List<Edge> cutEdgesA = new List<Edge>();
		List<Edge> cutEdgesB = new List<Edge>();
		List<Edge>[] result = new List<Edge>[2];

		foreach (Triangle triangle in triangles) {
			bool abovePlane0 = pointAbovePlane[triangle.point0.index];
			bool abovePlane1 = pointAbovePlane[triangle.point1.index];
			bool abovePlane2 = pointAbovePlane[triangle.point2.index];

			if (abovePlane0 && abovePlane1 && abovePlane2) {
				triangle.vertex0 = oldToNewVertex[triangle.vertex0];
				triangle.vertex1 = oldToNewVertex[triangle.vertex1];
				triangle.vertex2 = oldToNewVertex[triangle.vertex2];

				a.triangles.Add(triangle);
			}
			else if (!abovePlane0 && !abovePlane1 && !abovePlane2) {
				triangle.vertex0 = oldToNewVertex[triangle.vertex0];
				triangle.vertex1 = oldToNewVertex[triangle.vertex1];
				triangle.vertex2 = oldToNewVertex[triangle.vertex2];

				b.triangles.Add(triangle);
			}
			else {
				//Split triangle
				Point topPoint;
				Edge edge0, edge1, edge2;
				int vertex0, vertex1, vertex2;

				if (edgeIntersectsPlane[triangle.edge0.index] && edgeIntersectsPlane[triangle.edge1.index]) {
					topPoint = triangle.point1;
					edge0 = triangle.edge0;
					edge1 = triangle.edge1;
					edge2 = triangle.edge2;
					vertex0 = triangle.vertex0;
					vertex1 = triangle.vertex1;
					vertex2 = triangle.vertex2;
				}
				else if (edgeIntersectsPlane[triangle.edge1.index] && edgeIntersectsPlane[triangle.edge2.index]) {
					topPoint = triangle.point2;
					edge0 = triangle.edge1;
					edge1 = triangle.edge2;
					edge2 = triangle.edge0;
					vertex0 = triangle.vertex1;
					vertex1 = triangle.vertex2;
					vertex2 = triangle.vertex0;
				}
				else {
					topPoint = triangle.point0;
					edge0 = triangle.edge2;
					edge1 = triangle.edge0;
					edge2 = triangle.edge1;
					vertex0 = triangle.vertex2;
					vertex1 = triangle.vertex0;
					vertex2 = triangle.vertex1;
				}

				EdgeHit edgeHit0 = edgeHits[edge0.index];
				EdgeHit edgeHit1 = edgeHits[edge1.index];

				//Convert edge hit scalars
				float scalar0 = topPoint == edge0.point1 ? edgeHit0.scalar : 1.0f - edgeHit0.scalar;
				float scalar1 = topPoint == edge1.point0 ? edgeHit1.scalar : 1.0f - edgeHit1.scalar;

				Edge cutEdgeA, cutEdgeB;

				if (pointAbovePlane[topPoint.index]) {
					//Assign top triangle to A, bottom triangle to B
					cutEdgeA = new Edge(edgeHit1.splitA.point0, edgeHit0.splitA.point0);
					cutEdgeB = new Edge(edgeHit1.splitB.point0, edgeHit0.splitB.point0);

					a.edges.Add(cutEdgeA);
					b.edges.Add(cutEdgeB);

					SplitTriangle(a, b, edgeHit0.splitA, edgeHit1.splitA, cutEdgeA, edgeHit0.splitB, edgeHit1.splitB, cutEdgeB, edge2, vertex0, vertex1, vertex2, scalar0, scalar1, oldToNewVertex);
				}
				else {
					//Assign top triangle to B, bottom triangle to A
					cutEdgeA = new Edge(edgeHit0.splitA.point0, edgeHit1.splitA.point0);
					cutEdgeB = new Edge(edgeHit0.splitB.point0, edgeHit1.splitB.point0);

					a.edges.Add(cutEdgeA);
					b.edges.Add(cutEdgeB);

					SplitTriangle(b, a, edgeHit0.splitB, edgeHit1.splitB, cutEdgeB, edgeHit0.splitA, edgeHit1.splitA, cutEdgeA, edge2, vertex0, vertex1, vertex2, scalar0, scalar1, oldToNewVertex);
				}

				cutEdgesA.Add(cutEdgeA);
				cutEdgesB.Add(cutEdgeB);
			}
		}
		result[0] = cutEdgesA;
		result[1] = cutEdgesB;
		return result;
	}

	//TODO:fix pls
	private void HullifyTriangle(ConvexHull topHull, ConvexHull bottomHull, Edge topEdge0, Edge topEdge1, Edge topCutEdge, Edge bottomEdge0, Edge bottomEdge1, Edge bottomCutEdge, Edge bottomEdge2, int vertex0, int vertex1, int vertex2, float scalar0, float scalar1, int[] oldToNewVertex) {
		//Graphics Programmers on forums have --zero-- idea how to convey ideas to laymen. I found someone with
		//   basically my exact question, and the top rated response was this disaster. "I have a great book on this 
		//	 topic if you're interested!". Yeah, no thanks, I just want to split my triangle.
		//   http://stackoverflow.com/questions/24806221/split-a-triangle-into-smaller-triangles like why.
		//
		//Anyway, this is some sort of hybrid-inbred bastard child of Delaunay and StackOverflowUserBullshit with a few 
		//   genetic sprinkles of other random Math / Unity forums. 

		Vector3 n0 = normals[vertex0];
		Vector3 n1 = normals[vertex1];
		Vector3 n2 = normals[vertex2];

		Vector4 t0 = tangents[vertex0];
		Vector4 t1 = tangents[vertex1];
		Vector4 t2 = tangents[vertex2];

		//TODO: are we using UVs? Who knows!
		Vector2 uv0 = uvs[vertex0];
		Vector2 uv1 = uvs[vertex1];
		Vector2 uv2 = uvs[vertex2];

		//Calculate the cut vertex data by interpolating original triangle values
		Vector3 cutNormal0 = new Vector3();
		cutNormal0.x = n0.x + (n1.x - n0.x) * scalar0;
		cutNormal0.y = n0.y + (n1.y - n0.y) * scalar0;
		cutNormal0.z = n0.z + (n1.z - n0.z) * scalar0;
		cutNormal0.Normalize();

		Vector3 cutNormal1 = new Vector3();
		cutNormal1.x = n1.x + (n2.x - n1.x) * scalar1;
		cutNormal1.y = n1.y + (n2.y - n1.y) * scalar1;
		cutNormal1.z = n1.z + (n2.z - n1.z) * scalar1;
		cutNormal1.Normalize();

		Vector4 cutTangent0 = new Vector4();
		cutTangent0.x = t0.x + (t1.x - t0.x) * scalar0;
		cutTangent0.y = t0.y + (t1.y - t0.y) * scalar0;
		cutTangent0.z = t0.z + (t1.z - t0.z) * scalar0;
		cutTangent0.Normalize();
		cutTangent0.w = t0.w;

		Vector4 cutTangent1 = new Vector4();
		cutTangent1.x = t1.x + (t2.x - t1.x) * scalar1;
		cutTangent1.y = t1.y + (t2.y - t1.y) * scalar1;
		cutTangent1.z = t1.z + (t2.z - t1.z) * scalar1;
		cutTangent1.Normalize();
		cutTangent1.w = t1.w;

		//TODO: I put this here because the internet guide said to. Dunno if we'll use it.
		Vector2 cutUv0 = new Vector2();
		cutUv0.x = uv0.x + (uv1.x - uv0.x) * scalar0;
		cutUv0.y = uv0.y + (uv1.y - uv0.y) * scalar0;
		Vector2 cutUv1 = new Vector2();
		cutUv1.x = uv1.x + (uv2.x - uv1.x) * scalar1;
		cutUv1.y = uv1.y + (uv2.y - uv1.y) * scalar1;

		//Add the cut vertices to the hulls
		int topCutVertex0, topCutVertex1;
		topCutVertex0 = topHull.AddVertex(topEdge0.point0.position, cutNormal0, cutTangent0, cutUv0, topEdge0.point0);
		topCutVertex1 = topHull.AddVertex(topEdge1.point0.position, cutNormal1, cutTangent1, cutUv1, topEdge1.point0);

		int bottomCutVertex0, bottomCutVertex1;
		bottomCutVertex0 = bottomHull.AddVertex(bottomEdge0.point0.position, cutNormal0, cutTangent0, cutUv0, bottomEdge0.point0);
		bottomCutVertex1 = bottomHull.AddVertex(bottomEdge1.point0.position, cutNormal1, cutTangent1, cutUv1, bottomEdge1.point0);

		//Create the top of the original triangle
		Triangle topTriangle = new Triangle(topCutVertex0, oldToNewVertex[vertex1], topCutVertex1, topEdge0.point0, topEdge0.point1, topEdge1.point0, topEdge0, topEdge1, topCutEdge);
		topHull.triangles.Add(topTriangle);

		//Create the bottom of the original triangle
		Edge bottomCrossEdge = new Edge(bottomEdge0.point1, bottomEdge1.point0);

		Triangle bottomTriangle0 = new Triangle(oldToNewVertex[vertex0], bottomCutVertex0, bottomCutVertex1, bottomEdge0.point1, bottomEdge0.point0, bottomEdge1.point0, bottomEdge0, bottomCutEdge, bottomCrossEdge);
		Triangle bottomTriangle1 = new Triangle(oldToNewVertex[vertex0], bottomCutVertex1, oldToNewVertex[vertex2], bottomEdge0.point1, bottomEdge1.point0, bottomEdge1.point1, bottomCrossEdge, bottomEdge1, bottomEdge2);

		bottomHull.edges.Add(bottomCrossEdge);
		bottomHull.triangles.Add(bottomTriangle0);
		bottomHull.triangles.Add(bottomTriangle1);
	}

	private void SortCutEdges(IList<Edge> edgesA, IList<Edge> edgesB) {
		Edge start = edgesA[0];
		for (int i = 1; i < edgesA.Count; i++) {
			Edge previous = edgesA[i - 1];
			for (int j = i; j < edgesA.Count; j++) {
				Edge edgeA = edgesA[j];

				//Check if edge continues loop
				if (previous.point1 == edgeA.point0) {
					//Swap edges
					edgesA[i] = edgeA;
					edgesA[j] = edgesA[i];

					edgesB[i] = edgesB[j];
					edgesB[j] = edgesB[i];

					//Check if edge eneds loop
					if (edgeA.point1 == start.point0)
						start = null;

					break;
				}
			}
		}
	}


	private List< List< Edge > > SplitTriangle(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2) {
		//TODO: DAN SPLIT THIS SHIT YO
		Point pointyc = new Point ((vertex0 + vertex1 + vertex2) / 3f);
		Point pointy0 = new Point (vertex0);
		Point pointy1 = new Point (vertex1);
		Point pointy2 = new Point (vertex2);
		Edge edgey0 = new Edge (vertex0, pointyc);
		Edge edgey1 = new Edge (vertex1, pointyc);
		Edge edgey2 = new Edge (vertex2, pointyc);
		List<List<Edge>> resulty = new List<List<Edge>>();
		resulty.Add (new List<Edge> { edgey0, edgey1, new Edge(pointy0, pointy1) });
		resulty.Add (new List<Edge> { edgey1, edgey2, new Edge(pointy1, pointy2) });
		resulty.Add (new List<Edge> { edgey2, edgey0, new Edge(pointy2, pointy0) });
		return resulty;
	}
}


/* FOR CUTTING EDGES IN THE FUTURE
// Triangulate
Triangulation triangulator = new Triangulator(outlinePoints, outlineEdges, planeNormal);
List<int[]> edgesTrisTriedges = triangulator.Fill();
int[] newEdges = edgesTrisTriedges[0];
int[] newTriangles = edgesTrisTriedges[1];
int[] newTriangleEdges = edgesTrisTriedges[2];
 */ 