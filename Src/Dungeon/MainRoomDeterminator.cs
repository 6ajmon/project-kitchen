using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class MainRoomDeterminator : Node2D
{
    public int TileSize = 16;
    public float LargestRoomsPercent = 0.3f;  // 30% największych pokoi
    
    public (List<Rect2I>, List<Vector2I>) DetermineRooms(List<Rect2I> cells)
    {
        List<Rect2I> rooms = new List<Rect2I>();
        List<Vector2I> roomCenters = new List<Vector2I>();
        
        if (cells.Count == 0)
            return (rooms, roomCenters);
        
        // Sortowanie komórek według ich powierzchni (od największych do najmniejszych)
        var sortedCells = cells
            .Select(cell => new { Cell = cell, Area = cell.Size.X * cell.Size.Y })
            .OrderByDescending(item => item.Area)
            .ToList();
        
        // Obliczenie liczby pokoi do wybrania (30% całości, minimum 1)
        int roomCount = Math.Max(1, (int)(sortedCells.Count * LargestRoomsPercent));
        
        // Wybór najlepszych pokoi
        for (int i = 0; i < roomCount && i < sortedCells.Count; i++)
        {
            Rect2I room = sortedCells[i].Cell;
            rooms.Add(room);
            roomCenters.Add(room.Position + room.Size / 2);
        }
        
        GD.Print($"Selected {rooms.Count} rooms out of {cells.Count} cells ({LargestRoomsPercent*100}%)");
        
        return (rooms, roomCenters);
    }

    public int DetermineStartingRoom(List<Vector2I> roomCenters)
    {
        if (roomCenters.Count == 0)
            return -1;
        
        // Calculate the center point of all room centers
        Vector2I centerOfAllRooms = new Vector2I(0, 0);
        foreach (var center in roomCenters)
        {
            centerOfAllRooms += center;
        }
        centerOfAllRooms /= roomCenters.Count;
        
        // Find the room closest to the center
        int closestRoomIndex = 0;
        float closestDistance = float.MaxValue;
        
        for (int i = 0; i < roomCenters.Count; i++)
        {
            float distance = centerOfAllRooms.DistanceSquaredTo(roomCenters[i]);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closestRoomIndex = i;
            }
        }
        
        GD.Print($"Starting room determined: Room {closestRoomIndex} at {roomCenters[closestRoomIndex]}");
        return closestRoomIndex;
    }
}