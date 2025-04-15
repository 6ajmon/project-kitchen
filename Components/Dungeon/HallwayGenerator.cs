using Godot;
using System;
using System.Collections.Generic;

public partial class HallwayGenerator : Node2D
{
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    
    public override void _Ready()
    {
        _rng.Randomize();
    }
    
    public List<Rect2> CreateCorridors(List<Rect2> cells, List<Rect2> rooms, List<Vector2> roomCenters, List<(int, int)> corridorEdges)
    {
        List<Rect2> result = new List<Rect2>(rooms);
        
        foreach (var edge in corridorEdges)
        {
            Vector2 start = roomCenters[edge.Item1];
            Vector2 end = roomCenters[edge.Item2];
            
            // Determine whether to create an L-shaped corridor or straight line
            bool useLShape = _rng.Randf() > 0.5f;
            
            if (useLShape)
            {
                // Create L-shaped corridor
                Vector2 corner;
                
                if (_rng.Randf() > 0.5f)
                {
                    // Horizontal then vertical
                    corner = new Vector2(end.X, start.Y);
                }
                else
                {
                    // Vertical then horizontal
                    corner = new Vector2(start.X, end.Y);
                }
                
                // Apply corridor for both segments
                result = ApplyCorridorBetweenPoints(cells, result, start, corner);
                result = ApplyCorridorBetweenPoints(cells, result, corner, end);
            }
            else
            {
                // Create straight corridor
                result = ApplyCorridorBetweenPoints(cells, result, start, end);
            }
        }
        
        return result;
    }
    
    private List<Rect2> ApplyCorridorBetweenPoints(List<Rect2> cells, List<Rect2> rooms, Vector2 start, Vector2 end)
    {
        List<Rect2> result = new List<Rect2>(rooms);
        
        // Find all cells that intersect with this corridor line
        foreach (Rect2 cell in cells)
        {
            if (rooms.Contains(cell)) continue; // Skip rooms
            
            // Check if the cell intersects with the corridor line
            if (LineIntersectsRect(start, end, cell))
            {
                // Mark this cell as a corridor
                result.Add(cell); // For rendering purposes
            }
        }
        
        return result;
    }
    
    private bool LineIntersectsRect(Vector2 lineStart, Vector2 lineEnd, Rect2 rect)
    {
        // Simple check: does the line intersect any of the 4 sides of the rectangle?
        Vector2[] rectCorners = {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y)
        };
        
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            if (LineSegmentsIntersect(lineStart, lineEnd, rectCorners[i], rectCorners[next]))
            {
                return true;
            }
        }
        
        // Also check if either endpoint is inside the rectangle
        if (rect.HasPoint(lineStart) || rect.HasPoint(lineEnd))
        {
            return true;
        }
        
        return false;
    }
    
    private bool LineSegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        // Check if line segments AB and CD intersect
        float denominator = ((b.X - a.X) * (d.Y - c.Y)) - ((b.Y - a.Y) * (d.X - c.X));
        
        if (denominator == 0) return false; // Lines are parallel
        
        float numerator1 = ((a.Y - c.Y) * (d.X - c.X)) - ((a.X - c.X) * (d.Y - c.Y));
        float numerator2 = ((a.Y - c.Y) * (b.X - a.X)) - ((a.X - c.X) * (b.Y - a.Y));
        
        if (numerator1 == 0 || numerator2 == 0) return false; // Lines are coincident
        
        float r = numerator1 / denominator;
        float s = numerator2 / denominator;
        
        return (r > 0 && r < 1) && (s > 0 && s < 1);
    }
}