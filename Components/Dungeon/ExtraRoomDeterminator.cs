using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class ExtraRoomDeterminator : Node2D
{
    [Export] public int TileSize = 16;
    [Export] public float LargestExtraRoomsPercent = 0.3f;  // 30% of largest potential extra rooms

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
        // Consider a cell neighboring if it's directly adjacent to any dungeon room
        // (shares at least one tile edge, not just corners)
        
        foreach (var room in dungeonRooms)
        {
            // Check for horizontal adjacency (left or right side touching)
            bool horizontallyAdjacent = 
                // Cell's right edge touches room's left edge
                (cell.Position.X + cell.Size.X == room.Position.X &&
                 HasVerticalOverlap(cell, room)) ||
                // Cell's left edge touches room's right edge
                (cell.Position.X == room.Position.X + room.Size.X &&
                 HasVerticalOverlap(cell, room));
            
            // Check for vertical adjacency (top or bottom side touching)
            bool verticallyAdjacent = 
                // Cell's bottom edge touches room's top edge
                (cell.Position.Y + cell.Size.Y == room.Position.Y &&
                 HasHorizontalOverlap(cell, room)) ||
                // Cell's top edge touches room's bottom edge
                (cell.Position.Y == room.Position.Y + room.Size.Y &&
                 HasHorizontalOverlap(cell, room));
            
            if (horizontallyAdjacent || verticallyAdjacent)
                return true;
        }
        
        return false;
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
