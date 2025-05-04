using Godot;
using System;
using System.Collections.Generic;

public partial class HallwayGenerator : Node2D
{
    [Export] public int TileSize = 16;
    [Export] public int HallwayWidth = 2; // Width of hallways in tiles
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
            Vector2I start = roomCenters[edge.Item1];
            Vector2I end = roomCenters[edge.Item2];
            
            // Get original room rects
            Rect2I startRoom = rooms[edge.Item1];
            Rect2I endRoom = rooms[edge.Item2];
            
            // Calculate midpoint between rooms
            Vector2I midpoint = new Vector2I(
                (start.X + end.X) / 2,
                (start.Y + end.Y) / 2
            );
            
            // Check if midpoint x is within either room's x-range
            bool midXInStartRoom = midpoint.X >= startRoom.Position.X && midpoint.X <= startRoom.Position.X + startRoom.Size.X;
            bool midXInEndRoom = midpoint.X >= endRoom.Position.X && midpoint.X <= endRoom.Position.X + endRoom.Size.X;
            
            // Check if midpoint y is within either room's y-range
            bool midYInStartRoom = midpoint.Y >= startRoom.Position.Y && midpoint.Y <= startRoom.Position.Y + startRoom.Size.Y;
            bool midYInEndRoom = midpoint.Y >= endRoom.Position.Y && midpoint.Y <= endRoom.Position.Y + endRoom.Size.Y;
            
            // First try to create a straight corridor if possible
            if (midXInStartRoom && midXInEndRoom)
            {
                // Create vertical corridor at midpoint X
                Vector2I corridorStart = new Vector2I(midpoint.X, startRoom.Position.Y + startRoom.Size.Y / 2);
                Vector2I corridorEnd = new Vector2I(midpoint.X, endRoom.Position.Y + endRoom.Size.Y / 2);
                result = ApplyCorridorBetweenPoints(cells, result, corridorStart, corridorEnd);
            }
            else if (midYInStartRoom && midYInEndRoom)
            {
                // Create horizontal corridor at midpoint Y
                Vector2I corridorStart = new Vector2I(startRoom.Position.X + startRoom.Size.X / 2, midpoint.Y);
                Vector2I corridorEnd = new Vector2I(endRoom.Position.X + endRoom.Size.X / 2, midpoint.Y);
                result = ApplyCorridorBetweenPoints(cells, result, corridorStart, corridorEnd);
            }
            else
            {
                // Create L-shaped corridor
                Vector2I corner;
                
                // Try to use a midpoint if it falls within one of the rooms
                if (midXInStartRoom || midXInEndRoom)
                {
                    // Vertical then horizontal (use the midpoint X)
                    corner = new Vector2I(midpoint.X, end.Y);
                    Vector2I startPoint = new Vector2I(midpoint.X, start.Y);
                    result = ApplyCorridorBetweenPoints(cells, result, startPoint, corner);
                    result = ApplyCorridorBetweenPoints(cells, result, corner, end);
                }
                else if (midYInStartRoom || midYInEndRoom)
                {
                    // Horizontal then vertical (use the midpoint Y)
                    corner = new Vector2I(end.X, midpoint.Y);
                    Vector2I startPoint = new Vector2I(start.X, midpoint.Y);
                    result = ApplyCorridorBetweenPoints(cells, result, startPoint, corner);
                    result = ApplyCorridorBetweenPoints(cells, result, corner, end);
                }
                else
                {
                    // Default to room centers if no midpoint works
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
                    
                    result = ApplyCorridorBetweenPoints(cells, result, start, corner);
                    result = ApplyCorridorBetweenPoints(cells, result, corner, end);
                }
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
        int brushRadius = (HallwayWidth * TileSize) / 2; // Half width of our 2-tile corridor
        
        // Step 1: Find all cells that intersect with this corridor line or are within the brush radius
        foreach (Rect2I cell in cells)
        {
            if (rooms.Contains(cell)) continue; // Skip rooms
            
            // Check if the cell is close enough to the corridor line to be part of the "painted" hallway
            if (IsCellNearCorridorLine(start, end, cell, brushRadius))
            {
                // Mark this cell as a corridor
                if (!result.Contains(cell)) {
                    result.Add(cell);
                }
            }
        }
        
        // Step 2: Ensure there's a continuous corridor by directly painting tiles
        // This guarantees connectivity even if there are no cells in some areas
        PaintDirectCorridor(result, start, end);
        
        return result;
    }
    
    private void PaintDirectCorridor(List<Rect2I> result, Vector2I start, Vector2I end)
    {
        // Determine if we're moving mainly in X or Y direction
        bool horizontalFirst = Math.Abs(end.X - start.X) > Math.Abs(end.Y - start.Y);
        
        // Calculate corridor half-width in tiles
        int halfWidthInTiles = HallwayWidth / 2;
        int corridorWidth = HallwayWidth * TileSize;
        
        Vector2I corner;
        
        if (horizontalFirst) {
            // Create horizontal then vertical corridor
            corner = new Vector2I(end.X, start.Y);
            
            // Draw horizontal corridor (from start to corner)
            DrawStraightCorridor(result, start, corner, false, corridorWidth);
            
            // Draw vertical corridor (from corner to end)
            DrawStraightCorridor(result, corner, end, true, corridorWidth);
        }
        else {
            // Create vertical then horizontal corridor
            corner = new Vector2I(start.X, end.Y);
            
            // Draw vertical corridor (from start to corner) 
            DrawStraightCorridor(result, start, corner, true, corridorWidth);
            
            // Draw horizontal corridor (from corner to end)
            DrawStraightCorridor(result, corner, end, false, corridorWidth);
        }
    }
    
    private void DrawStraightCorridor(List<Rect2I> result, Vector2I start, Vector2I end, bool isVertical, int corridorWidth)
    {
        // Make sure start is before end in the appropriate dimension
        if ((isVertical && start.Y > end.Y) || (!isVertical && start.X > end.X)) {
            // Swap start and end if needed
            Vector2I temp = start;
            start = end;
            end = temp;
        }
        
        // Calculate the perpendicular range (half width on each side)
        int halfWidth = corridorWidth / 2;
        
        if (isVertical) {
            // Vertical corridor: X is fixed, Y varies
            int startY = start.Y / TileSize;
            int endY = end.Y / TileSize;
            int centerX = start.X / TileSize;
            
            for (int y = startY; y <= endY; y++) {
                // Draw corridor width
                for (int xOffset = -halfWidth/TileSize; xOffset <= halfWidth/TileSize; xOffset++) {
                    // Create a small cell for this tile position
                    Vector2I cellPos = new Vector2I((centerX + xOffset) * TileSize, y * TileSize);
                    Rect2I cell = new Rect2I(cellPos, new Vector2I(TileSize, TileSize));
                    
                    // Add to result if not already present
                    if (!ContainsOverlappingRect(result, cell)) {
                        result.Add(cell);
                    }
                }
            }
        }
        else {
            // Horizontal corridor: Y is fixed, X varies
            int startX = start.X / TileSize;
            int endX = end.X / TileSize;
            int centerY = start.Y / TileSize;
            
            for (int x = startX; x <= endX; x++) {
                // Draw corridor width
                for (int yOffset = -halfWidth/TileSize; yOffset <= halfWidth/TileSize; yOffset++) {
                    // Create a small cell for this tile position
                    Vector2I cellPos = new Vector2I(x * TileSize, (centerY + yOffset) * TileSize);
                    Rect2I cell = new Rect2I(cellPos, new Vector2I(TileSize, TileSize));
                    
                    // Add to result if not already present
                    if (!ContainsOverlappingRect(result, cell)) {
                        result.Add(cell);
                    }
                }
            }
        }
    }
    
    private bool ContainsOverlappingRect(List<Rect2I> rects, Rect2I newRect)
    {
        // Check if any of the existing rects fully contains the new rect
        foreach (var rect in rects) {
            if (rect.Position.X <= newRect.Position.X && 
                rect.Position.Y <= newRect.Position.Y &&
                rect.Position.X + rect.Size.X >= newRect.Position.X + newRect.Size.X &&
                rect.Position.Y + rect.Size.Y >= newRect.Position.Y + newRect.Size.Y) {
                return true;
            }
        }
        return false;
    }
    
    private bool IsCellNearCorridorLine(Vector2I lineStart, Vector2I lineEnd, Rect2I cell, float brushRadius)
    {
        // Convert to float for more accurate distance calculations
        Vector2 start = new Vector2(lineStart.X, lineStart.Y);
        Vector2 end = new Vector2(lineEnd.X, lineEnd.Y);
        
        // Get cell center
        Vector2 cellCenter = new Vector2(
            cell.Position.X + cell.Size.X / 2,
            cell.Position.Y + cell.Size.Y / 2
        );
        
        // Check if the center of the cell is within brush radius of the line
        if (DistanceFromPointToLine(cellCenter, start, end) <= brushRadius)
        {
            return true;
        }
        
        // Get rectangle corners
        Vector2[] rectCorners = {
            new Vector2(cell.Position.X, cell.Position.Y),
            new Vector2(cell.Position.X + cell.Size.X, cell.Position.Y),
            new Vector2(cell.Position.X + cell.Size.X, cell.Position.Y + cell.Size.Y),
            new Vector2(cell.Position.X, cell.Position.Y + cell.Size.Y)
        };
        
        // Check if any corner is within brush radius of the line
        foreach (var corner in rectCorners)
        {
            if (DistanceFromPointToLine(corner, start, end) <= brushRadius)
            {
                return true;
            }
        }
        
        // Check if line intersects with the rectangle directly
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            if (LineSegmentsIntersect(start, end, rectCorners[i], rectCorners[next]))
            {
                return true;
            }
        }
        
        // Also check if either endpoint is inside the rectangle
        Rect2 floatRect = new Rect2(cell.Position.X, cell.Position.Y, cell.Size.X, cell.Size.Y);
        if (floatRect.HasPoint(start) || floatRect.HasPoint(end))
        {
            return true;
        }
        
        return false;
    }
    
    private float DistanceFromPointToLine(Vector2 point, Vector2 lineStart, Vector2 lineEnd)
    {
        // Calculate the distance from a point to a line segment
        
        // Line vector
        Vector2 lineVec = lineEnd - lineStart;
        float lineLength = lineVec.Length();
        
        if (lineLength < 0.0001f)
        {
            // Line is actually a point
            return point.DistanceTo(lineStart);
        }
        
        // Normalize line vector
        Vector2 lineDir = lineVec / lineLength;
        
        // Vector from line start to point
        Vector2 startToPoint = point - lineStart;
        
        // Project startToPoint onto the line
        float projection = startToPoint.Dot(lineDir);
        
        // Get the closest point on the line
        Vector2 closestPoint;
        
        if (projection <= 0)
        {
            // Closest point is line start
            closestPoint = lineStart;
        }
        else if (projection >= lineLength)
        {
            // Closest point is line end
            closestPoint = lineEnd;
        }
        else
        {
            // Closest point is on the line
            closestPoint = lineStart + lineDir * projection;
        }
        
        // Return the distance to the closest point
        return point.DistanceTo(closestPoint);
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