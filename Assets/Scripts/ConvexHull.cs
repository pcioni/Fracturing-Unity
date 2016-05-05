using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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

	private void AddVertex(Vector3 vertex, Vector3 normal, Vector4 tangent, Vector2 uv, Point point) {
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

	public void Split(Vector3 localPointOnPlane, Vector3 localPlaneNormal, bool fillCut, UvMapper uvMapper, out Hull a, out Hull b) {
		if (localPlaneNormal == Vector3.zero) 
			localPlaneNormal = Vector3.up;

		a = new ConvexHull(this);
		b = new ConvexHull(this);

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

		AssignEdges(a, b, pointAbovePlane, localPointOnPlane, localPlaneNormal, out edgeIntersectsPlane, out edgeHits);

		IList<Edge> cutEdgesA, cutEdgesB;

		AssignTriangles(a, b, pointAbovePlane, edgeIntersectsPlane, edgeHits, oldToNewVertex, out cutEdgesA, out cutEdgesB);

		if (fillCut)
		{
			SortCutEdges(cutEdgesA, cutEdgesB);

			FillCutEdges(a, b, cutEdgesA, cutEdgesB, localPlaneNormal, uvMapper);
		}

		//TODO: might not need this
		//ValidateOutput(a, b, localPlaneNormal);

		//TODO: or this
		Clear();
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
				a.AddVertex(vertices[i], normals[i], tangents[i], uvs[i], correspondingPoint, out oldToNewVertex[i]);
			else
				b.AddVertex(vertices[i], normals[i], tangents[i], uvs[i], correspondingPoint, out oldToNewVertex[i]);
		}
		return oldToNewVertex;
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