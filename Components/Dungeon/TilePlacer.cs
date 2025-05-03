using Godot;
using System;
using System.Collections.Generic;

public partial class TilePlacer : Node
{
    [Export] public int TileSize = 16;
    
    private TileMapLayer _floorLayer;
    private TileMapLayer _wallLayer;
    
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
        // Generate a deterministic but seemingly random pattern based on coordinates
        int hash = (x * 7919) ^ (y * 6011); // Use prime numbers for better distribution
        Random random = new Random(hash);
        
        // Default floor tile with no walls (2,2)
        return new Vector2I(2, 2);
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
        // Determine wall position relative to the room
        bool isTop = y == topLeft.Y;
        bool isBottom = y == bottomRight.Y - 1;
        bool isLeft = x == topLeft.X;
        bool isRight = x == bottomRight.X - 1;
        
        // Generate a deterministic but pseudo-random variation
        int hash = (x * 7919) ^ (y * 6011);
        Random random = new Random(hash);
        
        // Corner cases - corners have 2 walls adjacent to each other
        // Diagonally swapped corners:
        // Top left → Bottom right (swap with 4,0)
        if (isTop && isLeft)
            return new Vector2I(4, 0); // Bottom right corner
        
        // Top right → Bottom left (swap with 5,0)
        if (isTop && isRight)
            return new Vector2I(5, 0); // Bottom left corner
        
        // Bottom left → Top right (swap with 4,1)
        if (isBottom && isLeft)
            return new Vector2I(4, 1); // Top right corner
        
        // Bottom right → Top left (swap with 5,1)
        if (isBottom && isRight)
            return new Vector2I(5, 1); // Top left corner
        
        // Edges - edges have one wall
        // Inverted: Top edge (floor from N) becomes Bottom edge (floor from S)
        if (isTop)
            return new Vector2I(1, 3); // Bottom edge - floor from S
        
        // Inverted: Bottom edge (floor from S) becomes Top edge (floor from N)
        if (isBottom)
            return new Vector2I(1, 1); // Top edge - floor from N
        
        // Inverted: Left edge (floor from W) becomes Right edge (floor from E)
        if (isLeft)
            return new Vector2I(2, 2); // Right edge - floor from E
        
        // Inverted: Right edge (floor from E) becomes Left edge (floor from W)
        if (isRight)
            return new Vector2I(0, 2); // Left edge - floor from W
        
        // Default case (shouldn't reach here)
        return new Vector2I(1, 2); // No floor around
    }
}
