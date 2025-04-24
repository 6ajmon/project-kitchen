using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GraphGenerator : Node2D
{
    [Export] public float LoopPercent = 0.1f; // Reduced from 0.15f to 0.1f (10%) for better dungeon layouts
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private List<(int, int, float)> _allEdges = new List<(int, int, float)>();
    private List<(int, int)> _mstEdges = new List<(int, int)>();
    private List<(int, int)> _corridorEdges = new List<(int, int)>();
    
    // Data structures for proper Delaunay triangulation
    private struct Edge : IEquatable<Edge>
    {
        public int P1;
        public int P2;

        public Edge(int p1, int p2)
        {
            // Normalize edge so that it's always the same regardless of direction
            if (p1 < p2)
            {
                P1 = p1;
                P2 = p2;
            }
            else
            {
                P1 = p2;
                P2 = p1;
            }
        }

        public bool Equals(Edge other)
        {
            return (P1 == other.P1 && P2 == other.P2);
        }

        public override bool Equals(object obj)
        {
            if (obj is Edge edge)
                return Equals(edge);
            return false;
        }

        public override int GetHashCode()
        {
            return P1.GetHashCode() ^ P2.GetHashCode();
        }
    }

    private class Triangle
    {
        public int A;
        public int B;
        public int C;
        public Vector2 CircumCenter;
        public float RadiusSquared;

        public Triangle(int a, int b, int c, List<Vector2I> points)
        {
            A = a;
            B = b;
            C = c;
            
            // Convert Vector2I to Vector2 for calculations
            Vector2 pointA = new Vector2(points[a].X, points[a].Y);
            Vector2 pointB = new Vector2(points[b].X, points[b].Y);
            Vector2 pointC = new Vector2(points[c].X, points[c].Y);
            
            // Calculate circumcenter
            ComputeCircumcenter(pointA, pointB, pointC);
            
            // Calculate squared radius
            RadiusSquared = (CircumCenter - pointA).LengthSquared();
        }
        
        public bool ContainsVertex(int v)
        {
            return v == A || v == B || v == C;
        }
        
        public bool CircumCircleContains(Vector2I point, List<Vector2I> points)
        {
            Vector2 p = new Vector2(point.X, point.Y);
            return (p - CircumCenter).LengthSquared() <= RadiusSquared;
        }
        
        private void ComputeCircumcenter(Vector2 a, Vector2 b, Vector2 c)
        {
            // Compute the perpendicular bisector of AB and BC
            Vector2 ab = b - a;
            Vector2 bc = c - b;
            
            // Midpoints
            Vector2 abMid = (a + b) / 2;
            Vector2 bcMid = (b + c) / 2;
            
            // Perpendicular vectors
            Vector2 abPerp = new Vector2(-ab.Y, ab.X);
            Vector2 bcPerp = new Vector2(-bc.Y, bc.X);
            
            // The circumcenter is the intersection of the perpendicular bisectors
            float t;
            if (Mathf.Abs(abPerp.X) > 0.00001f)
            {
                t = (bcMid.X - abMid.X + (bcPerp.Y / bcPerp.X) * (abMid.Y - bcMid.Y)) / 
                   (abPerp.Y - (abPerp.X * bcPerp.Y / bcPerp.X));
            }
            else
            {
                t = (bcMid.Y - abMid.Y) / bcPerp.X;
            }
            
            CircumCenter = abMid + abPerp * t;
        }
        
        public List<Edge> GetEdges()
        {
            return new List<Edge> 
            { 
                new Edge(A, B), 
                new Edge(B, C),
                new Edge(C, A)
            };
        }
    }
    
    public override void _Ready()
    {
        _rng.Randomize();
    }
    
    public List<(int, int)> GenerateDelaunayTriangulation(List<Vector2I> points)
    {
        if (points == null || points.Count < 2)
        {
            GD.PrintErr("Not enough points for triangulation");
            return new List<(int, int)>();
        }
        
        GD.Print($"Starting Delaunay triangulation with {points.Count} points");
        
        if (points.Count < 3) 
        {
            // If we have fewer than 3 points, just connect them all
            _allEdges.Clear();
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    float distance = new Vector2(points[i].X, points[i].Y).DistanceTo(new Vector2(points[j].X, points[j].Y));
                    _allEdges.Add((i, j, distance));
                }
            }
            var result = _allEdges.Select(e => (e.Item1, e.Item2)).ToList();
            GD.Print($"Created {result.Count} edges for {points.Count} points using direct connections");
            return result;
        }
        
        // Create a backup simple connection approach in case the triangulation fails
        List<(int, int, float)> backupEdges = new List<(int, int, float)>();
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                float distance = new Vector2(points[i].X, points[i].Y).DistanceTo(new Vector2(points[j].X, points[j].Y));
                backupEdges.Add((i, j, distance));
            }
        }
        
        try
        {
            // Find the bounding box
            int minX = points.Min(p => p.X);
            int minY = points.Min(p => p.Y);
            int maxX = points.Max(p => p.X);
            int maxY = points.Max(p => p.Y);
            
            // Calculate bounding box size with padding
            int dx = maxX - minX;
            int dy = maxY - minY;
            int deltaMax = Math.Max(dx, dy);
            int midX = (minX + maxX) / 2;
            int midY = (minY + maxY) / 2;
            
            // Add super-triangle vertices at the end of the points list
            // These will be removed at the end
            int startPointsCount = points.Count;
            var pointsCopy = new List<Vector2I>(points); // Create a copy to avoid modifying the original list
            pointsCopy.Add(new Vector2I(midX - 2 * deltaMax, midY - deltaMax));
            pointsCopy.Add(new Vector2I(midX, midY + 2 * deltaMax));
            pointsCopy.Add(new Vector2I(midX + 2 * deltaMax, midY - deltaMax));
            
            // Create initial super-triangle
            var triangulation = new List<Triangle>();
            triangulation.Add(new Triangle(startPointsCount, startPointsCount + 1, startPointsCount + 2, pointsCopy));
            
            // Add points one by one and retriangulate
            for (int i = 0; i < startPointsCount; i++)
            {
                var badTriangles = new List<Triangle>();
                
                // Find all triangles where the current point is inside their circumcircle
                foreach (var triangle in triangulation)
                {
                    if (triangle.CircumCircleContains(pointsCopy[i], pointsCopy))
                    {
                        badTriangles.Add(triangle);
                    }
                }
                
                // Find the boundary of the polygonal hole
                var polygon = new HashSet<Edge>();
                
                foreach (var triangle in badTriangles)
                {
                    foreach (var edge in triangle.GetEdges())
                    {
                        // If this edge is not shared by any other bad triangle,
                        // it's on the boundary of the hole
                        bool shared = false;
                        foreach (var otherTriangle in badTriangles)
                        {
                            if (otherTriangle == triangle) continue;
                            
                            var otherEdges = otherTriangle.GetEdges();
                            if (otherEdges.Contains(edge))
                            {
                                shared = true;
                                break;
                            }
                        }
                        
                        if (!shared)
                        {
                            polygon.Add(edge);
                        }
                        else
                        {
                            // If we already have this edge, remove it (shared edges cancel out)
                            polygon.Remove(edge);
                        }
                    }
                }
                
                // Remove bad triangles
                foreach (var triangle in badTriangles)
                {
                    triangulation.Remove(triangle);
                }
                
                // Retriangulate the hole by connecting each edge to the current point
                foreach (var edge in polygon)
                {
                    triangulation.Add(new Triangle(edge.P1, edge.P2, i, pointsCopy));
                }
            }
            
            GD.Print($"Triangulation complete with {triangulation.Count} triangles");
            
            // Extract edges from the final triangulation, ignore edges connected to super-triangle
            _allEdges.Clear();
            var edges = new HashSet<Edge>();
            int skippedTriangles = 0;
            
            foreach (var triangle in triangulation)
            {
                // Skip triangles connected to super-triangle
                if (triangle.A >= startPointsCount || triangle.B >= startPointsCount || triangle.C >= startPointsCount)
                {
                    skippedTriangles++;
                    continue;
                }
                
                foreach (var edge in triangle.GetEdges())
                {
                    edges.Add(edge);
                }
            }
            
            GD.Print($"Found {edges.Count} edges after skipping {skippedTriangles} triangles");
            
            // Convert edges to the format expected by the MST algorithm
            foreach (var edge in edges)
            {
                if (edge.P1 < 0 || edge.P1 >= points.Count || edge.P2 < 0 || edge.P2 >= points.Count)
                {
                    GD.PrintErr($"Invalid edge index: ({edge.P1}, {edge.P2}) - ignoring");
                    continue;
                }
                
                float distance = new Vector2(points[edge.P1].X, points[edge.P1].Y)
                    .DistanceTo(new Vector2(points[edge.P2].X, points[edge.P2].Y));
                _allEdges.Add((edge.P1, edge.P2, distance));
            }
            
            // Handle the case where triangulation produced no valid edges
            if (_allEdges.Count == 0)
            {
                GD.PrintErr("Triangulation produced no valid edges. Using backup approach.");
                _allEdges = backupEdges; // Use the backup approach
            }
            
            // Sort by distance for MST
            _allEdges.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            
            // Return all edges (we'll filter later for MST)
            var result = _allEdges.Select(e => (e.Item1, e.Item2)).ToList();
            GD.Print($"Delaunay triangulation created {result.Count} edges for {points.Count} points");
            return result;
        }
        catch (Exception e)
        {
            GD.PrintErr($"Error in triangulation: {e.Message}");
            GD.PrintErr($"Stack trace: {e.StackTrace}");
            
            // Use the backup connection approach
            _allEdges = backupEdges;
            
            // Sort by distance for MST
            _allEdges.Sort((a, b) => a.Item3.CompareTo(b.Item3));
            
            var result = _allEdges.Select(e => (e.Item1, e.Item2)).ToList();
            GD.Print($"Using fallback approach: created {result.Count} edges for {points.Count} points");
            return result;
        }
    }
    
    public List<(int, int)> GenerateMinimalSpanningTree(List<Vector2I> points)
    {
        if (points.Count < 2) 
        {
            GD.PrintErr("Not enough points for MST");
            return new List<(int, int)>();
        }
        
        if (_allEdges.Count == 0)
        {
            GD.PrintErr("No edges available for MST generation. Creating backup edges.");
            // Create a complete graph as backup
            for (int i = 0; i < points.Count; i++)
            {
                for (int j = i + 1; j < points.Count; j++)
                {
                    float distance = new Vector2(points[i].X, points[i].Y).DistanceTo(new Vector2(points[j].X, points[j].Y));
                    _allEdges.Add((i, j, distance));
                }
            }
            
            // Sort by distance for MST
            _allEdges.Sort((a, b) => a.Item3.CompareTo(b.Item3));
        }
        
        _mstEdges.Clear();
        int[] parent = new int[points.Count];
        
        // Initialize parents
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }
        
        // Process all edges to create MST (Kruskal's algorithm)
        foreach (var edge in _allEdges)
        {
            // Skip invalid edges
            if (edge.Item1 < 0 || edge.Item1 >= points.Count || 
                edge.Item2 < 0 || edge.Item2 >= points.Count)
            {
                continue;
            }
            
            int root1 = FindRoot(parent, edge.Item1);
            int root2 = FindRoot(parent, edge.Item2);
            
            if (root1 != root2)
            {
                _mstEdges.Add((edge.Item1, edge.Item2));
                parent[root1] = root2;
            }
        }
        
        // Ensure we have enough connections (at least points.Count-1)
        if (_mstEdges.Count < points.Count - 1 && points.Count >= 2)
        {
            GD.PrintErr($"MST incomplete. Only {_mstEdges.Count} edges for {points.Count} points (expected {points.Count-1})");
            
            // If MST is incomplete, ensure at least a minimum spanning path
            bool[] connected = new bool[points.Count];
            for (int i = 0; i < points.Count; i++)
            {
                connected[i] = false;
            }
            
            // Mark points that are already connected
            foreach (var edge in _mstEdges)
            {
                connected[edge.Item1] = true;
                connected[edge.Item2] = true;
            }
            
            // Find unconnected points and connect them to their nearest neighbor
            for (int i = 0; i < points.Count; i++)
            {
                if (!connected[i])
                {
                    // Find nearest connected point
                    int nearest = -1;
                    float minDist = float.MaxValue;
                    
                    for (int j = 0; j < points.Count; j++)
                    {
                        if (i != j && connected[j])
                        {
                            float dist = new Vector2(points[i].X, points[i].Y).DistanceTo(new Vector2(points[j].X, points[j].Y));
                            if (dist < minDist)
                            {
                                minDist = dist;
                                nearest = j;
                            }
                        }
                    }
                    
                    // If we found a connected point, connect to it
                    if (nearest != -1)
                    {
                        _mstEdges.Add((i, nearest));
                        connected[i] = true;
                    }
                    else if (points.Count > 0)
                    {
                        // If no connected points, connect to point 0
                        _mstEdges.Add((i, 0));
                        connected[i] = true;
                        connected[0] = true; 
                    }
                }
            }
        }
        
        GD.Print($"MST created with {_mstEdges.Count} edges connecting {points.Count} rooms");
        return _mstEdges;
    }
    
    public List<(int, int)> AddLoops()
    {
        _corridorEdges.Clear();
        _corridorEdges.AddRange(_mstEdges);
        
        // Find remaining edges (not in MST)
        List<(int, int, float)> remainingEdges = new List<(int, int, float)>();
        
        foreach (var edge in _allEdges)
        {
            bool inMst = false;
            
            // Check if edge is in MST (in either direction)
            foreach (var mstEdge in _mstEdges)
            {
                if ((edge.Item1 == mstEdge.Item1 && edge.Item2 == mstEdge.Item2) ||
                    (edge.Item1 == mstEdge.Item2 && edge.Item2 == mstEdge.Item1))
                {
                    inMst = true;
                    break;
                }
            }
            
            if (!inMst)
            {
                remainingEdges.Add(edge);
            }
        }
        
        // Sort by distance to prioritize shorter connections for loops
        remainingEdges.Sort((a, b) => a.Item3.CompareTo(b.Item3));
        
        // Add a percentage of the remaining edges
        int edgesToAdd = (int)(remainingEdges.Count * LoopPercent);
        
        // Ensure at least one loop if possible and desired
        if (edgesToAdd == 0 && remainingEdges.Count > 0 && LoopPercent > 0)
        {
            edgesToAdd = 1;
        }
        
        // Add edges, starting with shortest ones (most natural loops)
        for (int i = 0; i < edgesToAdd && i < remainingEdges.Count; i++)
        {
            _corridorEdges.Add((remainingEdges[i].Item1, remainingEdges[i].Item2));
        }
        
        GD.Print($"Added {edgesToAdd} loop connections ({LoopPercent*100}% of {remainingEdges.Count} possible loops)");
        return _corridorEdges;
    }
    
    public List<(int, int)> GetMSTEdges()
    {
        return new List<(int, int)>(_mstEdges);
    }
    
    private int FindRoot(int[] parent, int i)
    {
        if (parent[i] != i)
        {
            parent[i] = FindRoot(parent, parent[i]);
        }
        return parent[i];
    }
}