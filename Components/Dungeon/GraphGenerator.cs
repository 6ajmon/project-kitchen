using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class GraphGenerator : Node2D
{
    [Export] public float LoopPercent = 0.15f;
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private List<(int, int, float)> _allEdges = new List<(int, int, float)>();
    private List<(int, int)> _mstEdges = new List<(int, int)>();
    private List<(int, int)> _corridorEdges = new List<(int, int)>();
    
    public override void _Ready()
    {
        _rng.Randomize();
    }
    
    public List<(int, int)> GenerateDelaunayTriangulation(List<Vector2I> points)
    {
        if (points.Count < 2) return new List<(int, int)>();
        
        _allEdges.Clear();
        
        // Simplified Delaunay - connect all points and store distances
        for (int i = 0; i < points.Count; i++)
        {
            for (int j = i + 1; j < points.Count; j++)
            {
                // Use Vector2.DistanceTo by converting Vector2I to Vector2 for the distance calculation
                float distance = new Vector2(points[i].X, points[i].Y).DistanceTo(new Vector2(points[j].X, points[j].Y));
                _allEdges.Add((i, j, distance));
            }
        }
        
        // Sort edges by distance
        _allEdges.Sort((a, b) => a.Item3.CompareTo(b.Item3));
        
        // Return all edges (we'll filter later for MST)
        return _allEdges.Select(e => (e.Item1, e.Item2)).ToList();
    }
    
    public List<(int, int)> GenerateMinimalSpanningTree(List<Vector2I> points)
    {
        if (points.Count < 2) return new List<(int, int)>();
        
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
            int root1 = FindRoot(parent, edge.Item1);
            int root2 = FindRoot(parent, edge.Item2);
            
            if (root1 != root2)
            {
                _mstEdges.Add((edge.Item1, edge.Item2));
                parent[root1] = root2;
            }
        }
        
        return _mstEdges;
    }
    
    public List<(int, int)> AddLoops()
    {
        _corridorEdges.Clear();
        _corridorEdges.AddRange(_mstEdges);
        
        // Find remaining edges (not in MST)
        List<(int, int)> remainingEdges = new List<(int, int)>();
        
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
                remainingEdges.Add((edge.Item1, edge.Item2));
            }
        }
        
        // Shuffle remaining edges
        remainingEdges = remainingEdges.OrderBy(x => _rng.RandiRange(0, 1000)).ToList();
        
        // Add a percentage of the remaining edges
        int edgesToAdd = (int)(remainingEdges.Count * LoopPercent);
        
        for (int i = 0; i < edgesToAdd && i < remainingEdges.Count; i++)
        {
            _corridorEdges.Add(remainingEdges[i]);
        }
        
        return _corridorEdges;
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