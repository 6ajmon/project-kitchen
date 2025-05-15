using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonRenderer : Node2D
{
    public TileMapLayer WorldTileMap { get; set; }
    public TileMapLayer DisplayTileMap { get; set; }
    public int TileSize { get; set; } = 16;
    public Vector2I FloorAtlasCoord { get; set; } = new Vector2I(1, 0);
    public Vector2I WallAtlasCoord { get; set; } = new Vector2I(0, 0);
    
    private TilePlacer _tilePlacer;
    
    public override void _Ready()
    {
        _tilePlacer = GetNode<TilePlacer>("../TilePlacer");
        if (_tilePlacer == null)
        {
            GD.PrintErr("TilePlacer not found. Please check the node path.");
        }
    }
    
    // Updated method signature to include startingRoomIndex
    public void RenderDungeon(
        List<Rect2I> cells, 
        List<Rect2I> rooms, 
        List<Vector2I> roomCenters,
        List<Vector2I> corridorPositions = null,
        int startingRoomIndex = -1)
    {
        // Clear any existing tiles on the WorldTileMap
        WorldTileMap.Clear();
        
        // Create a set of all floor tile positions (in tile coordinates, not pixels)
        HashSet<Vector2I> floorPositions = new HashSet<Vector2I>();
        HashSet<Vector2I> wallPositions = new HashSet<Vector2I>();
        
        // Convert corridor positions from List to HashSet
        HashSet<Vector2I> corridorTiles = corridorPositions != null 
            ? new HashSet<Vector2I>(corridorPositions) 
            : new HashSet<Vector2I>();
        
        // First pass: identify main rooms and corridor cells
        List<Rect2I> mainRooms = new List<Rect2I>();
        List<Rect2I> corridorCells = new List<Rect2I>();
        
        foreach (var room in rooms)
        {
            if (IsCorridorCell(room))
            {
                corridorCells.Add(room);
            }
            else
            {
                mainRooms.Add(room);
            }
        }
        
        // Calculate the bounding box that contains all cells
        Rect2I boundingBox = CalculateDungeonBoundingBox(cells);
        
        // Second pass: add all room and corridor tiles to the floor positions
        foreach (var room in rooms)
        {
            // Convert pixel coordinates to tile coordinates
            int startTileX = room.Position.X / TileSize;
            int startTileY = room.Position.Y / TileSize;
            int widthInTiles = Mathf.CeilToInt((float)room.Size.X / TileSize);
            int heightInTiles = Mathf.CeilToInt((float)room.Size.Y / TileSize);
            
            // Iterate through tiles, not pixels
            for (int tileY = startTileY; tileY < startTileY + heightInTiles; tileY++)
            {
                for (int tileX = startTileX; tileX < startTileX + widthInTiles; tileX++)
                {
                    Vector2I tilePos = new Vector2I(tileX, tileY);
                    floorPositions.Add(tilePos);
                    
                    // Mark corridor tiles for later corridor opening detection
                    if (IsCorridorCell(room))
                    {
                        corridorTiles.Add(tilePos);
                    }
                }
            }
            
            // Only place walls around main rooms, not corridor cells
            if (!IsCorridorCell(room))
            {
                PlaceWallsAroundRoom(startTileX, startTileY, widthInTiles, heightInTiles, floorPositions, wallPositions);
            }
        }
        
        // Fill empty tiles with walls in the bounding box
        FillEmptySpacesWithWalls(boundingBox, floorPositions, wallPositions);
        
        // Find walls that should be corridor openings
        List<Vector2I> wallsToRemove = new List<Vector2I>();
        HashSet<Vector2I> openingsToAdd = new HashSet<Vector2I>();
        
        foreach (var wallPos in wallPositions)
        {
            if (ShouldBeCorridorOpening(wallPos, floorPositions, corridorTiles))
            {
                wallsToRemove.Add(wallPos);
                openingsToAdd.Add(wallPos);
            }
        }
        
        // Remove walls that should be openings
        foreach (var wallPos in wallsToRemove)
        {
            wallPositions.Remove(wallPos);
        }
        
        // Add as floor tiles
        foreach (var openingPos in openingsToAdd)
        {
            floorPositions.Add(openingPos);
        }
        
        // Place floor tiles
        foreach (var tilePos in floorPositions)
        {
            WorldTileMap.SetCell(tilePos, 0, FloorAtlasCoord);
        }
        
        // Place wall tiles at all wall positions
        foreach (var wallPos in wallPositions)
        {
            WorldTileMap.SetCell(wallPos, 0, WallAtlasCoord);
        }
        
        // Final pass: check for any corridor areas that need walls
        foreach (var floorTilePos in floorPositions)
        {
            // Check orthogonal neighbors (up, down, left, right)
            Vector2I[] neighbors = {
                new Vector2I(floorTilePos.X, floorTilePos.Y - 1), // Up
                new Vector2I(floorTilePos.X, floorTilePos.Y + 1), // Down
                new Vector2I(floorTilePos.X - 1, floorTilePos.Y), // Left
                new Vector2I(floorTilePos.X + 1, floorTilePos.Y)  // Right
            };
            
            foreach (var neighborTilePos in neighbors)
            {
                if (!floorPositions.Contains(neighborTilePos) && !wallPositions.Contains(neighborTilePos))
                {
                    // This is a wall position that hasn't been placed yet
                    WorldTileMap.SetCell(neighborTilePos, 0, WallAtlasCoord);
                    wallPositions.Add(neighborTilePos);
                }
            }
        }
        
        // Special handling for the starting room (e.g., place a player marker)
        if (startingRoomIndex >= 0 && startingRoomIndex < roomCenters.Count)
        {
            Vector2I startRoomCenter = roomCenters[startingRoomIndex];
            
            // Here you could add special tiles or markers to the starting room
            // For example, a special floor tile or a player spawn point
        }
        
        // Update display tiles using the TilePlacer
        if (_tilePlacer != null)
        {
            _tilePlacer.UpdateDisplayTiles(WorldTileMap, DisplayTileMap);
        }
        else
        {
            GD.PrintErr("TilePlacer reference is null. Cannot update display tiles.");
        }
    }
    
    // Identify if a cell is a corridor cell (small cell)
    private bool IsCorridorCell(Rect2I cell)
    {
        // Corridor cells are typically small, single-tile cells
        int minWidthInPixels = 6 * TileSize;
        int minHeightInPixels = 6 * TileSize;
        
        // Check if this is a corridor cell (small) vs a main room (large)
        bool isSmallCell = cell.Size.X < minWidthInPixels || cell.Size.Y < minHeightInPixels;
        
        return isSmallCell;
    }
    
    private Rect2I CalculateDungeonBoundingBox(List<Rect2I> cells)
    {
        if (cells.Count == 0)
            return new Rect2I(0, 0, 0, 0);
            
        // Find min and max coordinates to determine bounds
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = int.MinValue;
        int maxY = int.MinValue;
        
        foreach (var cell in cells)
        {
            minX = Math.Min(minX, cell.Position.X);
            minY = Math.Min(minY, cell.Position.Y);
            maxX = Math.Max(maxX, cell.Position.X + cell.Size.X);
            maxY = Math.Max(maxY, cell.Position.Y + cell.Size.Y);
        }
        
        // Convert to tile coordinates and add padding (1 tile on each side)
        int borderPadding = 1;
        int startTileX = (minX / TileSize) - borderPadding;
        int startTileY = (minY / TileSize) - borderPadding;
        int endTileX = Mathf.CeilToInt((float)maxX / TileSize) + borderPadding;
        int endTileY = Mathf.CeilToInt((float)maxY / TileSize) + borderPadding;
        
        int widthInTiles = endTileX - startTileX;
        int heightInTiles = endTileY - startTileY;
        
        return new Rect2I(startTileX, startTileY, widthInTiles, heightInTiles);
    }
    
    private void FillEmptySpacesWithWalls(Rect2I boundingBox, HashSet<Vector2I> floorPositions, HashSet<Vector2I> wallPositions)
    {
        // Iterate through every tile position in the bounding box
        for (int tileY = boundingBox.Position.Y; tileY < boundingBox.Position.Y + boundingBox.Size.Y; tileY++)
        {
            for (int tileX = boundingBox.Position.X; tileX < boundingBox.Position.X + boundingBox.Size.X; tileX++)
            {
                Vector2I tilePos = new Vector2I(tileX, tileY);
                
                // If the position is not already a floor or wall, make it a wall
                if (!floorPositions.Contains(tilePos) && !wallPositions.Contains(tilePos))
                {
                    wallPositions.Add(tilePos);
                }
            }
        }
    }

    private void PlaceWallsAroundRoom(int startX, int startY, int width, int height, HashSet<Vector2I> floorPositions, HashSet<Vector2I> wallPositions)
    {
        // Place walls around the room's perimeter including corners
        
        // Top and bottom walls (including corners)
        for (int x = startX - 1; x <= startX + width; x++)
        {
            // Top wall
            Vector2I topPos = new Vector2I(x, startY - 1);
            if (!floorPositions.Contains(topPos)) {
                wallPositions.Add(topPos);
            }
            
            // Bottom wall
            Vector2I bottomPos = new Vector2I(x, startY + height);
            if (!floorPositions.Contains(bottomPos)) {
                wallPositions.Add(bottomPos);
            }
        }
        
        // Left and right walls (excluding corners which were handled above)
        for (int y = startY; y < startY + height; y++)
        {
            // Left wall
            Vector2I leftPos = new Vector2I(startX - 1, y);
            if (!floorPositions.Contains(leftPos)) {
                wallPositions.Add(leftPos);
            }
            
            // Right wall
            Vector2I rightPos = new Vector2I(startX + width, y);
            if (!floorPositions.Contains(rightPos)) {
                wallPositions.Add(rightPos);
            }
        }
    }

    // Detect corridor openings
    private bool ShouldBeCorridorOpening(Vector2I wallPos, HashSet<Vector2I> floorPositions, HashSet<Vector2I> corridorPositions)
    {
        // A position should be a corridor opening if it's adjacent to both:
        // 1. A corridor floor tile
        // 2. A non-corridor floor tile (main room)
        
        bool adjacentToCorridor = false;
        bool adjacentToRoom = false;
        
        // Check all four adjacent positions
        Vector2I[] adjacentPositions = {
            new Vector2I(wallPos.X, wallPos.Y - 1), // Up
            new Vector2I(wallPos.X, wallPos.Y + 1), // Down
            new Vector2I(wallPos.X - 1, wallPos.Y), // Left
            new Vector2I(wallPos.X + 1, wallPos.Y)  // Right
        };
        
        foreach (var adjPos in adjacentPositions)
        {
            if (floorPositions.Contains(adjPos))
            {
                if (corridorPositions.Contains(adjPos))
                {
                    adjacentToCorridor = true;
                }
                else
                {
                    adjacentToRoom = true;
                }
            }
        }
        
        // This wall should be a corridor opening if it's adjacent to both
        // a corridor tile and a main room tile
        return adjacentToCorridor && adjacentToRoom;
    }
}
