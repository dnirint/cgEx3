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

        // Your implementation here...

        return new QuadMeshData();
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
                    float a = intersection[0];
                    float b = intersection[1];
                    sharedEdges.Add(new Vector2(Math.Min(a, b), Math.Max(a, b)));
                    Vector4 edgeVector = new Vector4(intersection[0], intersection[1], Math.Max(i, j), Math.Min(i, j));
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
        }
        return outList;
    }

    // Returns a list of "edge points" for the given CCMeshData, as described in the Catmull-Clark algorithm 
    public static List<Vector3> GetEdgePoints(CCMeshData mesh)
    {
        return null;
    }

    // Returns a list of new locations of the original points for the given CCMeshData, as described in the CC algorithm 
    public static List<Vector3> GetNewPoints(CCMeshData mesh)
    {
        return null;
    }
}
