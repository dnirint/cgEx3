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
        // Create the rest of the new points and generate the new QuadMeshData object that we return
        return GetNewQuadMeshData(meshData);
    }

   
    // Compares two Vector2's and hashes them.
    // two vectors that have the same values (even in different positions) are considered the same.
    // assumes that the Vector2's represent Vectors of indices of vertices (each Vector2 is actually a pair of vertice indices representing an edge).
    public class EdgeComparer : EqualityComparer<Vector2>
    {
        public override bool Equals(Vector2 vec1, Vector2 vec2)
        {
            if (vec1.x == vec2.x)
            {
                return vec1.y == vec2.y;
            }
            if (vec1.y == vec2.x)
            {
                return vec1.x == vec2.y;
            }
            return false;
        }

        public override int GetHashCode(Vector2 obj)
        {
            // compute some number to spread out the keys across the hash table
            return (int)obj.magnitude;
        }
    }

    // Returns a list of all edges in the mesh defined by given points and faces.
    // Each edge is represented by Vector4(p1, p2, f1, f2)
    // p1, p2 are the edge vertices
    // f1, f2 are faces incident to the edge. If the edge belongs to one face only, f2 is -1
    public static List<Vector4> GetEdges(CCMeshData mesh)
    { 
        // generate a dictionary that maps an edge to the faces it touches
        Dictionary<Vector2, int[]> knownEdges = new Dictionary<Vector2, int[]>(new EdgeComparer());
        for (int i = 0; i < mesh.faces.Count(); i++)
        {
            Vector4 face = mesh.faces[i];
            for (int k = 0; k < 4; k++)
            {
                Vector2 curEdge = new Vector2(face[k], face[(k + 1) % 4]);
                if (knownEdges.ContainsKey(curEdge))
                {
                    knownEdges[curEdge][1] = i;
                }
                else
                {
                    knownEdges.Add(curEdge, new int[] { i, -1 });
                }
            }
        }
        // generate a list of Vector4's where xy are an edge and zw are the faces the edge touches
        List<Vector4> outList = new List<Vector4>();
        foreach (KeyValuePair<Vector2, int[]> item in knownEdges)
        {
            outList.Add(new Vector4(item.Key.x, item.Key.y, item.Value[0], item.Value[1]));
        }
        return outList;

    }    

    // Returns a list of "face points" for the given CCMeshData, as described in the Catmull-Clark algorithm 
    public static List<Vector3> GetFacePoints(CCMeshData mesh)
    {
        List<Vector3> outList = new List<Vector3>();
        mesh.faces.ForEach(face => outList.Add((mesh.points[(int)face.x] + mesh.points[(int)face.y] + mesh.points[(int)face.z] + mesh.points[(int)face.w]) / 4));
        return outList;
    }

    // Returns a list of "edge points" for the given CCMeshData, as described in the Catmull-Clark algorithm 
    public static List<Vector3> GetEdgePoints(CCMeshData mesh)
    {
        List<Vector3> outList = new List<Vector3>();
        foreach (Vector4 edge in mesh.edges)
        {
            Vector3 avg = mesh.points[(int)edge[0]] + mesh.points[(int)edge[1]] + mesh.facePoints[(int)edge[2]];
            if (edge[3] != -1)
            {
                outList.Add((avg + mesh.facePoints[(int)edge[3]]) / 4);
            }
            else // the current edge touches only 1 face
            {
                outList.Add(avg / 3);
            }
        }   
        return outList;
    }


    public class NewPointData
    {
        public Vector3 coordinates;
        public List<int> edges;
        public List<int> faces;

        public NewPointData(Vector3 coordinates)
        {
            this.coordinates = coordinates;
            this.edges = new List<int>();
            this.faces = new List<int>();
        }
    }



    // Returns a list of new locations of the original points for the given CCMeshData, as described in the CC algorithm 
    public static List<Vector3> GetNewPoints(CCMeshData mesh)
    {
        List<Vector3> outList = new List<Vector3>();
        List<NewPointData> newPointsData = new List<NewPointData>();

        // prepare an entry for each point
        mesh.points.ForEach(pointCoordinates => newPointsData.Add(new NewPointData(pointCoordinates)));
        // collect the data for each new point
        for (int i=0; i<mesh.edges.Count(); i++)
        {
            // each edge is made of two points p1 and p2 and is between two faces f1 and f2
            Vector4 edge = mesh.edges[i];
            int p1 = (int) edge[0];
            int p2 = (int) edge[1];
            int f1 = (int) edge[2];
            int f2 = (int) edge[3];
            // add the first face (f1) to the relevant points data
            newPointsData[p1].faces.Add(f1);
            newPointsData[p2].faces.Add(f1);
            // if the second face (f2) exists, add it as well
            if (f2 != -1)
            {
                newPointsData[p1].faces.Add(f2);
                newPointsData[p2].faces.Add(f2);
            }
            // add the current edge to the new points data
            newPointsData[p1].edges.Add(i);
            newPointsData[p2].edges.Add(i);
        }

        foreach (NewPointData newPointData in newPointsData)
        {
            // compute the average of the facePoints surrounding the current point
            Vector3 f = Vector3.zero;
            newPointData.faces.ForEach(faceIndex => f += mesh.facePoints[faceIndex]);
            f /= newPointData.faces.Count();

            int n = newPointData.edges.Count();

            // compute the average of edge mid-points for the current point's surrounding edges
            Vector3 r = Vector3.zero;
            foreach(int edgeIndex in newPointData.edges)
            {
                Vector4 edge = mesh.edges[edgeIndex];
                var p1 = mesh.points[(int)edge.x];
                var p2 = mesh.points[(int)edge.y];
                r += (p1 + p2);
            }
            r /= (n);
            Vector3 p = newPointData.coordinates;
            // compute the location of the new point according to the formula we saw in class
            Vector3 newPointLocation = (f + (r) + ((n - 3) * p)) / n;
            outList.Add(newPointLocation);
        }

        return outList;
    }

    public static QuadMeshData GetNewQuadMeshData(CCMeshData meshData)
    {
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
            edgesByVertex[(int)edge.x].Add(i);
            edgesByVertex[(int)edge.y].Add(i);
        }


        List<Vector3> newVertices = new List<Vector3>(meshData.newPoints);
        List<Vector4> newQuads = new List<Vector4>();
        Dictionary<Vector3, int> edgePointIndices = new Dictionary<Vector3, int>();
        int newPointIndex = newVertices.Count();

        for (int i = 0; i < meshData.faces.Count(); i++)
        {
            /* Terminology:
             * For each face we create 4 new faces, where each new face consists of 4 vertices (wlog, name them a,b,c,d and they are ordered clockwise).
             * Each of these 4 subfaces looks like this (we can rotate them clockwise but this is easier to use):
             * a = a vertex from the original face, in it's new location
             * b = edge point (relevant to the edge going out from a)
             * c = face point of the current face (the original face)
             * d = edge point (relevant to the edge going into a) 
             */
            var curFace = meshData.faces[i];
            // the face point stays constant, so we add it to the new vertices list
            Vector3 c = meshData.facePoints[i];
            newVertices.Add(c);
            int c_index = newPointIndex++;
            // iterate over the 4 possible a vertices (in other words, create a smalle face for each of the original vertices in the original face)
            for (int j = 0; j < 4; j++)
            {
                int a = (int)curFace[j];
                int a_next = (int)curFace[(j + 1) % 4];
                int a_prev = (int)curFace[(j + 3) % 4];

                int b_index = -1;
                int d_index = -1;
                // iterate over the edges that touch the current vertex to find the incoming and outgoing edges
                // for each edge, add the other vertex of the edge to the newVertices list and update the edgePointIndices dict so that the next time
                // we would know not to add the same vertex to the newVertices again, thus avoiding duplicate entries while still keeping track of each vertex' index.
                foreach (int edgeIndex in edgesByVertex[a])
                {
                    Vector4 edge = meshData.edges[edgeIndex];
                    // current edge goes out from a to a_next
                    if (a_next == edge.x || a_next == edge.y)
                    {
                        Vector3 b = meshData.edgePoints[edgeIndex];
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
                    // current edge goes in from a_prev to a
                    else if (a_prev == edge.x || a_prev == edge.y)
                    {
                        Vector3 d = meshData.edgePoints[edgeIndex];
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
                Vector4 newFace = new Vector4(a, b_index, c_index, d_index);
                newQuads.Add(newFace);
            }
        }

        return new QuadMeshData(newVertices, newQuads);
    }

}
