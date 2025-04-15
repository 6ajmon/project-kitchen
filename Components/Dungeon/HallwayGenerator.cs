using Godot;
using System;
using System.Collections.Generic;

public partial class HallwayGenerator : Node2D
{
    [Export] public int TileSize = 16;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    
    public override void _Ready()
    {
        _rng.Randomize();
    }
    
    public List<Rect2I> CreateCorridors(List<Rect2I> cells, List<Rect2I> rooms, List<Vector2I> roomCenters, List<(int, int)> corridorEdges)
    {
        List<Rect2I> result = new List<Rect2I>(rooms);
        
        foreach (var edge in corridorEdges)
        {
            Vector2I start = SnapToGrid(roomCenters[edge.Item1]);
            Vector2I end = SnapToGrid(roomCenters[edge.Item2]);
            
            // Determine whether to create an L-shaped corridor or straight line
            bool useLShape = _rng.Randf() > 0.5f;
            
            if (useLShape)
            {
                // Create L-shaped corridor
                Vector2I corner;
                
                if (_rng.Randf() > 0.5f)
                {
                    // Horizontal then vertical
                    corner = new Vector2I(end.X, start.Y);
                }
                else
                {
                    // Vertical then horizontal
                    corner = new Vector2I(start.X, end.Y);
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
    
    private Vector2I SnapToGrid(Vector2I position)
    {
        return new Vector2I(
            (position.X / TileSize) * TileSize,
            (position.Y / TileSize) * TileSize
        );
    }
    
    private List<Rect2I> ApplyCorridorBetweenPoints(List<Rect2I> cells, List<Rect2I> rooms, Vector2I start, Vector2I end)
    {
        List<Rect2I> result = new List<Rect2I>(rooms);
        
        // Find all cells that intersect with this corridor line
        foreach (Rect2I cell in cells)
        {
            if (rooms.Contains(cell)) continue; // Skip rooms
            
            // Check if the cell intersects with the corridor line
            if (LineIntersectsRect(start, end, cell))
            {
                // Mark this cell as a corridor
                result.Add(cell);
            }
        }
        
        return result;
    }
    
    private bool LineIntersectsRect(Vector2I lineStart, Vector2I lineEnd, Rect2I rect)
    {
        // Convert to float to use line segment intersection logic
        Vector2 start = new Vector2(lineStart.X, lineStart.Y);
        Vector2 end = new Vector2(lineEnd.X, lineEnd.Y);
        
        // Get rectangle corners
        Vector2[] rectCorners = {
            new Vector2(rect.Position.X, rect.Position.Y),
            new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y),
            new Vector2(rect.Position.X + rect.Size.X, rect.Position.Y + rect.Size.Y),
            new Vector2(rect.Position.X, rect.Position.Y + rect.Size.Y)
        };
        
        // Check if line intersects any of the rectangle sides
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            if (LineSegmentsIntersect(start, end, rectCorners[i], rectCorners[next]))
            {
                return true;
            }
        }
        
        // Also check if either endpoint is inside the rectangle
        Rect2 floatRect = new Rect2(rect.Position.X, rect.Position.Y, rect.Size.X, rect.Size.Y);
        if (floatRect.HasPoint(start) || floatRect.HasPoint(end))
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