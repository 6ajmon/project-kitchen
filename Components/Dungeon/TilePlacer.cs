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
    
    // Dictionary to store wall positions for fast lookup (for floor tile selection)
    private Dictionary<Vector2I, bool> _wallPositions = new Dictionary<Vector2I, bool>();
    
    // Wall detection directions
    private readonly Vector2I[] _directions = new Vector2I[]
    {
        new Vector2I(0, -1),  // N
        new Vector2I(1, -1),  // NE
        new Vector2I(1, 0),   // E
        new Vector2I(1, 1),   // SE
        new Vector2I(0, 1),   // S
        new Vector2I(-1, 1),  // SW
        new Vector2I(-1, 0),  // W
        new Vector2I(-1, -1)  // NW
    };

    // Wall indices for easier reference
    private const int N = 0;
    private const int NE = 1;
    private const int E = 2;
    private const int SE = 3;
    private const int S = 4;
    private const int SW = 5;
    private const int W = 6;
    private const int NW = 7;
    
    public void Initialize(TileMapLayer floorLayer, TileMapLayer wallLayer)
    {
        _floorLayer = floorLayer;
        _wallLayer = wallLayer;
        _floorPositions.Clear();
        _wallPositions.Clear();
    }
    
    public void ClearTiles()
    {
        if (_floorLayer != null) _floorLayer.Clear();
        if (_wallLayer != null) _wallLayer.Clear();
        _floorPositions.Clear();
        _wallPositions.Clear();
    }
    
    public void PlaceTiles(List<Rect2I> rooms)
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
        // Pre-populate wall positions for better floor tile selection
        PrecomputeWallPositions(rooms);
        
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
                    
                    // Skip if there's a wall here
                    if (_wallPositions.ContainsKey(pos)) continue;
                    
                    // Choose appropriate floor tile based on surrounding walls
                    Vector2I atlasCoord = GetFloorTileVariation(pos);
                    
                    // Place floor tile and record its position
                    _floorLayer.SetCell(pos, 2, atlasCoord, 0); // Use source_id 2 for floor tileset
                    _floorPositions[pos] = true;
                }
            }
        }
    }
    
    private void PrecomputeWallPositions(List<Rect2I> rooms)
    {
        _wallPositions.Clear();
        
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
            
            // Mark wall positions
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    bool isEdge = x == topLeft.X || x == bottomRight.X - 1 || 
                                 y == topLeft.Y || y == bottomRight.Y - 1;
                    
                    if (isEdge)
                    {
                        _wallPositions[new Vector2I(x, y)] = true;
                    }
                }
            }
        }
    }
    
    private Vector2I GetFloorTileVariation(Vector2I pos)
    {
        // Check for walls in all 8 directions
        bool[] wallsAround = new bool[8];
        
        for (int i = 0; i < _directions.Length; i++)
        {
            Vector2I checkPos = pos + _directions[i];
            wallsAround[i] = _wallPositions.ContainsKey(checkPos);
        }
        
        // Now determine the proper floor tile based on wall configuration
        
        // Check if it's an isle (surrounded by walls)
        if (CountWallsAround(wallsAround) == 8)
        {
            return new Vector2I(4, 0); // Isle
        }
        
        // Check for specific wall configurations
        if (wallsAround[N] && wallsAround[W])
        {
            return new Vector2I(1, 1); // Walls on N and W
        }
        if (wallsAround[N] && !wallsAround[W] && !wallsAround[E])
        {
            return new Vector2I(2, 1); // Wall on N
        }
        if (wallsAround[N] && wallsAround[E])
        {
            return new Vector2I(3, 1); // Walls on N and E
        }
        if (!wallsAround[N] && !wallsAround[S] && wallsAround[W])
        {
            return new Vector2I(1, 2); // Wall on W
        }
        if (!wallsAround[N] && !wallsAround[S] && wallsAround[E])
        {
            return new Vector2I(3, 2); // Wall on E
        }
        if (wallsAround[W] && wallsAround[S])
        {
            return new Vector2I(1, 3); // Walls on W and S
        }
        if (wallsAround[S] && !wallsAround[W] && !wallsAround[E])
        {
            return new Vector2I(2, 3); // Wall on S
        }
        if (wallsAround[S] && wallsAround[E])
        {
            return new Vector2I(3, 3); // Walls on S and E
        }
        
        // Check for corner walls
        if (wallsAround[SE])
        {
            return new Vector2I(5, 0); // Wall on SE
        }
        if (wallsAround[SW])
        {
            return new Vector2I(6, 0); // Wall on SW
        }
        if (wallsAround[NE])
        {
            return new Vector2I(5, 1); // Wall on NE
        }
        if (wallsAround[NW])
        {
            return new Vector2I(6, 1); // Wall on NW
        }
        
        // Check for double-corner walls
        if (wallsAround[NE] && wallsAround[SE])
        {
            return new Vector2I(5, 2); // Walls on NE and SE
        }
        if (wallsAround[NW] && wallsAround[NE])
        {
            return new Vector2I(6, 2); // Walls on NE and NW
        }
        if (wallsAround[NW] && wallsAround[SW])
        {
            return new Vector2I(5, 3); // Walls on NW and SW
        }
        if (wallsAround[SW] && wallsAround[SE])
        {
            return new Vector2I(6, 3); // Walls on SE and SW
        }
        
        // Check for row patterns
        if (wallsAround[W] && !wallsAround[E] && !wallsAround[N] && !wallsAround[S])
        {
            return new Vector2I(1, 0); // Row left
        }
        if (wallsAround[E] && !wallsAround[W] && !wallsAround[N] && !wallsAround[S])
        {
            return new Vector2I(3, 0); // Row right
        }
        
        // Check for column patterns
        if (wallsAround[N] && !wallsAround[S])
        {
            return new Vector2I(4, 1); // Column upper
        }
        if (wallsAround[N] && wallsAround[S])
        {
            return new Vector2I(4, 2); // Column center
        }
        if (!wallsAround[N] && wallsAround[S])
        {
            return new Vector2I(4, 3); // Column lower
        }
        
        // Default - no walls around
        return new Vector2I(2, 2);
    }
    
    private int CountWallsAround(bool[] wallsAround)
    {
        int count = 0;
        foreach (bool hasWall in wallsAround)
        {
            if (hasWall) count++;
        }
        return count;
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
