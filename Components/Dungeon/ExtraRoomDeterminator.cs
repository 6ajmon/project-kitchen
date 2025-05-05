using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ExtraRoomDeterminator : Node2D
{
    public int TileSize = 16;
    public float LargestExtraRoomsPercent = 1.0f;
    public int NeighborDistance = 2; // Number of tiles to consider as "neighboring"

    public (List<Rect2I>, List<Vector2I>) DetermineExtraRooms(List<Rect2I> allCells, List<Rect2I> dungeonRooms)
    {
        List<Rect2I> extraRooms = new List<Rect2I>();
        List<Vector2I> extraRoomCenters = new List<Vector2I>();

        if (allCells.Count == 0 || dungeonRooms.Count == 0)
            return (extraRooms, extraRoomCenters);

        // Step 1: Find all cells that are not already part of the dungeon
        var availableCells = allCells
            .Where(cell => !IsPartOfDungeon(cell, dungeonRooms))
            .ToList();

        if (availableCells.Count == 0)
            return (extraRooms, extraRoomCenters);

        // Step 2: Find cells that are directly neighboring the dungeon
        var neighboringCells = availableCells
            .Where(cell => IsNeighboringDungeon(cell, dungeonRooms))
            .Select(cell => new { Cell = cell, Area = cell.Size.X * cell.Size.Y })
            .OrderByDescending(item => item.Area)
            .ToList();

        // Step 3: Select largest rooms (30% of neighboring cells)
        int extraRoomCount = Math.Max(1, (int)(neighboringCells.Count * LargestExtraRoomsPercent));
        
        for (int i = 0; i < extraRoomCount && i < neighboringCells.Count; i++)
        {
            Rect2I room = neighboringCells[i].Cell;
            extraRooms.Add(room);
            extraRoomCenters.Add(room.Position + room.Size / 2);
        }

        GD.Print($"Selected {extraRooms.Count} potential extra rooms from {neighboringCells.Count} neighboring cells");
        
        return (extraRooms, extraRoomCenters);
    }

    private bool IsPartOfDungeon(Rect2I cell, List<Rect2I> dungeonRooms)
    {
        // Check if the cell is already part of the dungeon (exact match or fully contained)
        foreach (var room in dungeonRooms)
        {
            // Check for exact match
            if (cell.Position == room.Position && cell.Size == room.Size)
                return true;
                
            // Check if cell is fully contained within a dungeon room
            if (room.Position.X <= cell.Position.X &&
                room.Position.Y <= cell.Position.Y &&
                room.Position.X + room.Size.X >= cell.Position.X + cell.Size.X &&
                room.Position.Y + room.Size.Y >= cell.Position.Y + cell.Size.Y)
                return true;
        }
        
        return false;
    }

    private bool IsNeighboringDungeon(Rect2I cell, List<Rect2I> dungeonRooms)
    {
        // Consider a cell neighboring if it's within a certain distance of the dungeon
        // The distance is measured in tiles (NeighborDistance parameter)
        int proximityThreshold = TileSize * NeighborDistance;
        
        // Convert to Rect2 for easier distance calculations
        Rect2 cellRect = new Rect2(cell.Position.X, cell.Position.Y, cell.Size.X, cell.Size.Y);
        
        foreach (var room in dungeonRooms)
        {
            // Convert room to Rect2 as well
            Rect2 roomRect = new Rect2(room.Position.X, room.Position.Y, room.Size.X, room.Size.Y);
            
            // Get the closest points between the two rectangles
            Vector2 closestPointInCell = GetClosestPoint(cellRect, roomRect.GetCenter());
            Vector2 closestPointInRoom = GetClosestPoint(roomRect, cellRect.GetCenter());
            
            // Calculate distance between closest points
            float distance = closestPointInCell.DistanceTo(closestPointInRoom);
            
            // If the distance is within our threshold, consider it neighboring
            if (distance <= proximityThreshold)
            {
                return true;
            }
            
            // Also check if they're still directly adjacent (which might still happen sometimes)
            bool horizontallyAdjacent = 
                // Cell's right edge touches or is near room's left edge
                (Math.Abs(cell.Position.X + cell.Size.X - room.Position.X) <= proximityThreshold &&
                 HasVerticalOverlap(cell, room)) ||
                // Cell's left edge touches or is near room's right edge
                (Math.Abs(cell.Position.X - (room.Position.X + room.Size.X)) <= proximityThreshold &&
                 HasVerticalOverlap(cell, room));
            
            bool verticallyAdjacent = 
                // Cell's bottom edge touches or is near room's top edge
                (Math.Abs(cell.Position.Y + cell.Size.Y - room.Position.Y) <= proximityThreshold &&
                 HasHorizontalOverlap(cell, room)) ||
                // Cell's top edge touches or is near room's bottom edge
                (Math.Abs(cell.Position.Y - (room.Position.Y + room.Size.Y)) <= proximityThreshold &&
                 HasHorizontalOverlap(cell, room));
            
            if (horizontallyAdjacent || verticallyAdjacent)
                return true;
        }
        
        return false;
    }
    
    private Vector2 GetClosestPoint(Rect2 rect, Vector2 point)
    {
        // Find the closest point on/in the rectangle to the given point
        float x = Mathf.Clamp(point.X, rect.Position.X, rect.Position.X + rect.Size.X);
        float y = Mathf.Clamp(point.Y, rect.Position.Y, rect.Position.Y + rect.Size.Y);
        return new Vector2(x, y);
    }
    
    private bool HasVerticalOverlap(Rect2I rect1, Rect2I rect2)
    {
        // Check if there's any vertical overlap between two rectangles
        return Math.Max(rect1.Position.Y, rect2.Position.Y) < 
               Math.Min(rect1.Position.Y + rect1.Size.Y, rect2.Position.Y + rect2.Size.Y);
    }
    
    private bool HasHorizontalOverlap(Rect2I rect1, Rect2I rect2)
    {
        // Check if there's any horizontal overlap between two rectangles
        return Math.Max(rect1.Position.X, rect2.Position.X) < 
               Math.Min(rect1.Position.X + rect1.Size.X, rect2.Position.X + rect2.Size.X);
    }
}
