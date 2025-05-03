using Godot;
using System;
using System.Collections.Generic;

public partial class TilePlacer : Node
{
    [Export] public int TileSize = 16;
    
    private TileMapLayer _floorLayer;
    private TileMapLayer _wallLayer;
    
    // Floor tileset is 4x7 (1 + 4x6)
    private const int FLOOR_ATLAS_WIDTH = 4;
    private const int FLOOR_ATLAS_HEIGHT = 7;
    
    // Wall tileset is 4x6
    private const int WALL_ATLAS_WIDTH = 4;
    private const int WALL_ATLAS_HEIGHT = 6;
    
    // Dictionary to store floor positions for fast lookup
    private Dictionary<Vector2I, bool> _floorPositions = new Dictionary<Vector2I, bool>();
    
    public void Initialize(TileMapLayer floorLayer, TileMapLayer wallLayer)
    {
        _floorLayer = floorLayer;
        _wallLayer = wallLayer;
        _floorPositions.Clear();
    }
    
    public void ClearTiles()
    {
        if (_floorLayer != null) _floorLayer.Clear();
        if (_wallLayer != null) _wallLayer.Clear();
        _floorPositions.Clear();
    }
    
    public void PlaceTiles(List<Rect2I> rooms, float searchRadius)
    {
        // Clear existing tiles
        ClearTiles();
        
        // First place floor tiles
        PlaceFloorTiles(rooms);
        
        // Then place wall tiles inside rooms (instead of around them)
        PlaceInnerWallTiles(rooms);
    }
    
    private void PlaceFloorTiles(List<Rect2I> rooms)
    {
        foreach (Rect2I room in rooms)
        {
            // Convert room coordinates to tile coordinates
            Vector2I topLeft = new Vector2I(
                room.Position.X / TileSize,
                room.Position.Y / TileSize
            );
            
            Vector2I bottomRight = new Vector2I(
                (room.Position.X + room.Size.X) / TileSize,
                (room.Position.Y + room.Size.Y) / TileSize
            );
            
            // Place floor tiles with variations
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    
                    // Choose a random floor tile for variation
                    Vector2I atlasCoord = GetFloorTileVariation(x, y, topLeft, bottomRight);
                    
                    // Place floor tile and record its position
                    _floorLayer.SetCell(pos, 2, atlasCoord, 0); // Use source_id 2 for floor tileset
                    _floorPositions[pos] = true;
                }
            }
        }
    }
    
    private Vector2I GetFloorTileVariation(int x, int y, Vector2I topLeft, Vector2I bottomRight)
    {
        // Handle edge cases differently than center tiles for more natural looking dungeon
        bool isEdge = x == topLeft.X || x == bottomRight.X - 1 || y == topLeft.Y || y == bottomRight.Y - 1;
        
        // Generate a deterministic but seemingly random pattern based on coordinates
        int hash = (x * 7919) ^ (y * 6011); // Use prime numbers for better distribution
        Random random = new Random(hash);
        
        // Based on atlas description:
        // Primary floor tiles are at (2,2) = no walls, and variations
        if (isEdge)
        {
            // Edge tiles - use variations with walls on appropriate sides
            // For now, use simple floor tiles
            return new Vector2I(2, 2); // Basic floor tile with no walls
        }
        else
        {
            // Center areas - use clean floor tiles
            return new Vector2I(2, 2); // Basic floor tile with no walls
        }
    }
    
    private void PlaceInnerWallTiles(List<Rect2I> rooms)
    {
        foreach (Rect2I room in rooms)
        {
            // Convert room coordinates to tile coordinates
            Vector2I topLeft = new Vector2I(
                room.Position.X / TileSize,
                room.Position.Y / TileSize
            );
            
            Vector2I bottomRight = new Vector2I(
                (room.Position.X + room.Size.X) / TileSize,
                (room.Position.Y + room.Size.Y) / TileSize
            );
            
            // Build walls on the edges of the room
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    
                    // Check if the tile is at the edge of the room
                    bool isEdge = x == topLeft.X || x == bottomRight.X - 1 || 
                                 y == topLeft.Y || y == bottomRight.Y - 1;
                    
                    if (isEdge)
                    {
                        // Remove floor tile (if exists) and mark this position as non-floor
                        if (_floorPositions.ContainsKey(pos))
                        {
                            _floorPositions.Remove(pos);
                            _floorLayer.EraseCell(pos);
                        }
                        
                        // Determine wall type based on its position in the room
                        Vector2I wallType = DetermineWallType(x, y, topLeft, bottomRight);
                        
                        // Place the wall
                        _wallLayer.SetCell(pos, 0, wallType, 0);
                    }
                }
            }
        }
    }
    
    private Vector2I DetermineWallType(int x, int y, Vector2I topLeft, Vector2I bottomRight)
    {
        // Determine the wall's position relative to the room
        bool isTop = y == topLeft.Y;
        bool isBottom = y == bottomRight.Y - 1;
        bool isLeft = x == topLeft.X;
        bool isRight = x == bottomRight.X - 1;
        
        // Generate deterministic but seemingly random variation
        int hash = (x * 7919) ^ (y * 6011);
        Random random = new Random(hash);
        
        // Corners
        if (isTop && isLeft)
            return new Vector2I(1, 1); // Top-left corner - walls on N and W
        
        if (isTop && isRight)
            return new Vector2I(3, 1); // Top-right corner - walls on N and E
        
        if (isBottom && isLeft)
            return new Vector2I(1, 3); // Bottom-left corner - walls on S and W
        
        if (isBottom && isRight)
            return new Vector2I(3, 3); // Bottom-right corner - walls on S and E
        
        // Edges
        if (isTop)
            return new Vector2I(2, 1); // Top edge - wall on N
        
        if (isBottom)
            return new Vector2I(2, 3); // Bottom edge - wall on S
        
        if (isLeft)
            return new Vector2I(1, 2); // Left edge - wall on W
        
        if (isRight)
            return new Vector2I(3, 2); // Right edge - wall on E
        
        // Default, though we shouldn't reach here
        return new Vector2I(random.Next(0, WALL_ATLAS_WIDTH), random.Next(0, 2));
    }
    
    private void PlaceWallTiles(float searchRadius)
    {
        // This method is no longer used as we now build walls inside rooms
    }
    
    private int CountAdjacentFloors(Vector2I pos)
    {
        int count = 0;
        
        // Check all 8 surrounding tiles
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                
                Vector2I checkPos = pos + new Vector2I(dx, dy);
                if (_floorPositions.ContainsKey(checkPos))
                {
                    count++;
                }
            }
        }
        
        return count;
    }
    
    private Vector2I GetWallTileType(Vector2I pos)
    {
        // This method is no longer used as we now build walls inside rooms
        return new Vector2I(0, 0);
    }
}
