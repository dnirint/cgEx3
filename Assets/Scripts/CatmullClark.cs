using System;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;


public class CCMeshData
{
    public List<Vector3> points; // Original mesh points
    public List<Vector4> faces; // Original mesh quad faces
    public List<Vector4> edges; // Original mesh edges
    public List<Vector3> facePoints; // Face points, as described in the Catmull-Clark algorithm
    public List<Vector3> edgePoints; // Edge points, as described in the Catmull-Clark algorithm
    public List<Vector3> newPoints; // New locations of the original mesh points, according to Catmull-Clark
}



public static class CatmullClark
{
    // Returns a QuadMeshData representing the input mesh after one iteration of Catmull-Clark subdivision.
    public static QuadMeshData Subdivide(QuadMeshData quadMeshData)
    {
        // Create and initialize a CCMeshData corresponding to the given QuadMeshData
        CCMeshData meshData = new CCMeshData();
        meshData.points = quadMeshData.vertices;
        meshData.faces = quadMeshData.quads;
        meshData.edges = GetEdges(meshData);
        meshData.facePoints = GetFacePoints(meshData);
        meshData.edgePoints = GetEdgePoints(meshData);
        meshData.newPoints = GetNewPoints(meshData);

        // Combine facePoints, edgePoints and newPoints into a subdivided QuadMeshData

        // We create a list with length meshData.points.Count() which maps each vertex to its neighbor edges
        List<List<int>> edgesByVertex = new List<List<int>>();
        for (int i = 0; i < meshData.points.Count(); i++)
        {
            edgesByVertex.Add(new List<int>());
        }

        for (int i = 0; i < meshData.edges.Count(); i++)
        {
            Vector4 edge = meshData.edges[i];
            edgesByVertex[(int) edge.x].Add(i);
            edgesByVertex[(int) edge.y].Add(i);
        }


        List<Vector3> newVertices = new List<Vector3>(meshData.newPoints);
        Dictionary<Vector3, int> edgePointIndices = new Dictionary<Vector3, int>();
        int newPointIndex = newVertices.Count();
        var newQuads = new List<Vector4>();
        for (int i = 0; i < meshData.faces.Count(); i++)
        {
            Vector3 c = meshData.facePoints[i];
            newVertices.Add(c);
            int c_index = newPointIndex++;
            for (int j = 0; j < 4; j++)
            {

                int a = (int) meshData.faces[i][j];

                int a_next = (int) meshData.faces[i][(j + 1) % 4];
                int a_prev = (int) meshData.faces[i][(j + 3) % 4];

                Vector3 b = Vector4.zero;
                Vector3 d = Vector4.zero;
                int b_index = -1;
                int d_index = -1;
                foreach (int edgeIndex in edgesByVertex[a])
                {
                    Vector4 edge = meshData.edges[edgeIndex];
                    if (a_next == edge.x || a_next == edge.y)
                    {
                        b = meshData.edgePoints[edgeIndex];
                        if (!edgePointIndices.ContainsKey(b))
                        {
                            newVertices.Add(b);
                            b_index = newPointIndex;
                            edgePointIndices.Add(b, newPointIndex++);
                        }
                        else
                        {
                            b_index = edgePointIndices[b];
                        }
                    }
                    if (a_prev == edge.x || a_prev == edge.y)
                    {
                        d = meshData.edgePoints[edgeIndex];
                        if (!edgePointIndices.ContainsKey(d))
                        {
                            newVertices.Add(d);
                            d_index = newPointIndex;
                            edgePointIndices.Add(d, newPointIndex++);
                        }
                        else
                        {
                            d_index = edgePointIndices[d];
                        }
                    }
                    
                }
                if (d_index< 0 || b_index < 0)
                {
                    throw new Exception("Unexpected result");
                }
                Vector4 newFace = new Vector4(a, b_index, c_index, d_index);
                newQuads.Add(newFace);
            }
        }
        return new QuadMeshData(newVertices, newQuads);
    }

    // Returns a list of all edges in the mesh defined by given points and faces.
    // Each edge is represented by Vector4(p1, p2, f1, f2)
    // p1, p2 are the edge vertices
    // f1, f2 are faces incident to the edge. If the edge belongs to one face only, f2 is -1
    public static List<Vector4> GetEdges(CCMeshData mesh)
    {
        //List<Vector4> outList = new List<Vector4>();
        HashSet<Vector4> outList = new HashSet<Vector4>();

        for (int i=0; i<mesh.faces.Count(); i++)
        {
            Vector4 face = mesh.faces[i];
            float[] verts = new float[4] { face.x, face.y, face.z, face.w };

            List<Vector2> faceEdges = new List<Vector2>();
            for (int f=0; f<verts.Length; f++)
            {
                float a = face[f];
                float b = face[(f + 1) % 4];
                faceEdges.Add(new Vector2(Math.Min(a, b), Math.Max(a, b)));
            }
            HashSet<Vector2> sharedEdges = new HashSet<Vector2>();
            // add all adjacent faces to the out vector list
            for (int j = 0; j < mesh.faces.Count(); j++)
            {
                Vector4 otherFace = mesh.faces[j];
                float[] otherVerts = new float[4] { otherFace.x, otherFace.y, otherFace.z, otherFace.w };
                var intersection = verts.Intersect(otherVerts).ToArray();
                
                if (intersection.Count() == 2)
                {
                    float a = Math.Min(intersection[0], intersection[1]);
                    float b = Math.Max(intersection[0], intersection[1]);
                    sharedEdges.Add(new Vector2(a, b));
                    Vector4 edgeVector = new Vector4(a, b, Math.Max(i, j), Math.Min(i, j));
                    outList.Add(edgeVector);
                }
            }
            // here we check if the current face has less than 4 adjacent faces (and if so we add a -1 index as otherFace)
            foreach(Vector2 edge in faceEdges)
            {
                if (! sharedEdges.Contains(edge))
                {
                    outList.Add(new Vector4(edge.x, edge.y, i, -1));
                }
            }
        }
        return outList.ToList();
    }

    // Returns a list of "face points" for the given CCMeshData, as described in the Catmull-Clark algorithm 
    public static List<Vector3> GetFacePoints(CCMeshData mesh)
    {
        List<Vector3> outList = new List<Vector3>();
        foreach (Vector4 face in mesh.faces)
        {
            List<float> pointIndices = new float[4] { face.x, face.y, face.z, face.w }.ToList();
            List<Vector3> subList = mesh.points.Where((item, index) => pointIndices.Contains(index)).ToList();
            var average = subList.Aggregate(new Vector3(0, 0, 0), (s, v) => s + v) / (float)subList.Count;
            outList.Add(average);
        }
        return outList;
    }

    // Returns a list of "edge points" for the given CCMeshData, as described in the Catmull-Clark algorithm 
    public static List<Vector3> GetEdgePoints(CCMeshData mesh)
    {
        List<Vector3> outList = new List<Vector3>();
        foreach (Vector4 edge in mesh.edges)
        {
            //Vector4(p1, p2, f1, f2)
            var p1 = mesh.points[(int) edge[0]];
            var p2 = mesh.points[(int) edge[1]];
            var f1 = mesh.facePoints[(int) edge[2]];
            Vector4 newEdgePoint = Vector4.zero;
            if (edge[3] != -1)
            {
                var f2 = mesh.facePoints[(int) edge[3]];
                newEdgePoint = (p1 + p2 + f1 + f2) / 4;
            }
            else
            {
                var f2 = Vector4.zero;
                newEdgePoint = (p1 + p2 + f1) / 3;
            }

            outList.Add(newEdgePoint);
        }   
        return outList;
    }


    public struct NewPointData
    {
        public Vector3 coordinates;
        public List<int> edges;
        public List<int> faces;
    }


    // Returns a list of new locations of the original points for the given CCMeshData, as described in the CC algorithm 
    public static List<Vector3> GetNewPoints(CCMeshData mesh)
    {
        List<Vector3> outList = new List<Vector3>();
        List<NewPointData> newPointsData = new List<NewPointData>();
        
        //Populating newPointsData
        foreach (Vector3 point in mesh.points)
        {
            NewPointData data = new NewPointData() {coordinates=point, edges = new List<int>(), faces = new List<int>()};
            newPointsData.Add(data);
        }
        for (int i=0; i<mesh.edges.Count(); i++)
        {
            Vector4 edge = mesh.edges[i];
            int p1 = (int) edge[0];
            int p2 = (int) edge[1];
            int f1 = (int) edge[2];
            int f2 = (int) edge[3];

            newPointsData[p1].faces.Add(f1);
            newPointsData[p2].faces.Add(f1);

            if (f2 != -1)
            {
                newPointsData[p1].faces.Add(f2);
                newPointsData[p2].faces.Add(f2);
            }

            newPointsData[p1].edges.Add(i);
            newPointsData[p2].edges.Add(i);
            int k = 10;
            if (p1 == k || p2 == k)
            {
                Debug.Log($"vertex {k} participated in edge {i}");
            }

        }

        foreach (NewPointData newPointData in newPointsData)
        {
            Vector3 f = Vector3.zero;
            foreach(int faceIndex in newPointData.faces)
            {
                f += mesh.facePoints[faceIndex];
            }
            f /= (float)newPointData.faces.Count();

            Vector3 r = Vector3.zero;
            foreach(int edgeIndex in newPointData.edges)
            {
                Vector4 edge = mesh.edges[edgeIndex]; //Vector4(p1, p2, f1, f2)
                var p1 = mesh.points[(int)edge[0]];
                var p2 = mesh.points[(int)edge[1]];
                r += (p1 + p2) / (float)2;
            }
            int n = newPointData.edges.Count();
            //if (n != 3)
            //{
            //    Debug.Log($"n={n}");
            //}
            r /= n;
            Vector3 p = newPointData.coordinates;
            Vector3 newPointLocation = (f + (2 * r) + ((n - 3) * p)) / (float)n;
            outList.Add(newPointLocation);
        }

        return outList;
    }
}
