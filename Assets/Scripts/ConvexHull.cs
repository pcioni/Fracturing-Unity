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

}


/* FOR CUTTING EDGES IN THE FUTURE
// Triangulate
Triangulation triangulator = new Triangulator(outlinePoints, outlineEdges, planeNormal);
List<int[]> edgesTrisTriedges = triangulator.Fill();
int[] newEdges = edgesTrisTriedges[0];
int[] newTriangles = edgesTrisTriedges[1];
int[] newTriangleEdges = edgesTrisTriedges[2];
 */ 