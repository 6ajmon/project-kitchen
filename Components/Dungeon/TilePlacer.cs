using Godot;
using System;
using System.Collections.Generic;

public partial class TilePlacer : Node
{
    [Export] public int TileSize = 16;
    
    private TileMapLayer _floorLayer;
    private TileMapLayer _wallLayer;
    
    // Dictionaries and sets for tile tracking
    private Dictionary<Vector2I, bool> _floorPositions = new Dictionary<Vector2I, bool>();
    private Dictionary<Vector2I, bool> _wallPositions = new Dictionary<Vector2I, bool>();
    private HashSet<Vector2I> _hallwayPositions = new HashSet<Vector2I>();
    private HashSet<Vector2I> _hallwayEdges = new HashSet<Vector2I>();
    
    // Minimum dimensions for a room (in tiles)
    private const int MinRoomWidth = 5;
    private const int MinRoomHeight = 5;
    
    // Direction vectors for checking surrounding tiles
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

    // Direction indices
    private const int N = 0;
    private const int NE = 1;
    private const int E = 2;
    private const int SE = 3;
    private const int S = 4;
    private const int SW = 5;
    private const int W = 6;
    private const int NW = 7;
    
    // Cardinal directions for hallway edge detection
    private readonly Vector2I[] _cardinalDirections = new Vector2I[]
    {
        new Vector2I(0, -1),  // North
        new Vector2I(1, 0),   // East
        new Vector2I(0, 1),   // South
        new Vector2I(-1, 0),  // West
    };
    
    public void Initialize(TileMapLayer floorLayer, TileMapLayer wallLayer)
    {
        _floorLayer = floorLayer;
        _wallLayer = wallLayer;
        ClearData();
    }
    
    public void ClearTiles()
    {
        if (_floorLayer != null) _floorLayer.Clear();
        if (_wallLayer != null) _wallLayer.Clear();
        ClearData();
    }
    
    private void ClearData()
    {
        _floorPositions.Clear();
        _wallPositions.Clear();
        _hallwayPositions.Clear();
        _hallwayEdges.Clear();
    }
    
    public void PlaceTiles(List<Rect2I> rooms)
    {
        ClearTiles();
        IdentifyHallways(rooms);
        IdentifyHallwayEdges();
        PrecomputeWallPositions(rooms);
        PlaceFloorTiles(rooms);
        PlaceInnerWallTiles(rooms);
        PlaceHallwayEdgeWalls();
    }
    
    private void IdentifyHallways(List<Rect2I> rooms)
    {
        _hallwayPositions.Clear();
        
        HashSet<Vector2I> roomCells = new HashSet<Vector2I>();
        List<Rect2I> mainRooms = GetMainRooms(rooms);
        
        // Add all cells from main rooms to the room cells set
        foreach (var room in mainRooms)
        {
            Vector2I topLeft = GetTilePosition(room.Position);
            Vector2I bottomRight = GetTilePosition(room.Position + room.Size);
            
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    roomCells.Add(new Vector2I(x, y));
                }
            }
        }
        
        // Identify hallways (cells in small rooms that aren't in main rooms)
        foreach (var room in rooms)
        {
            if (mainRooms.Contains(room))
                continue;
            
            Vector2I topLeft = GetTilePosition(room.Position);
            Vector2I bottomRight = GetTilePosition(room.Position + room.Size);
            
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    if (!roomCells.Contains(pos))
                    {
                        _hallwayPositions.Add(pos);
                    }
                }
            }
        }
        
        GD.Print($"Identified {_hallwayPositions.Count} hallway cells for floor placement");
    }
    
    private List<Rect2I> GetMainRooms(List<Rect2I> rooms)
    {
        List<Rect2I> mainRooms = new List<Rect2I>();
        
        foreach (var room in rooms)
        {
            int widthInTiles = room.Size.X / TileSize;
            int heightInTiles = room.Size.Y / TileSize;
            
            if (widthInTiles >= MinRoomWidth && heightInTiles >= MinRoomHeight)
            {
                mainRooms.Add(room);
            }
        }
        
        return mainRooms;
    }
    
    private Vector2I GetTilePosition(Vector2I position)
    {
        return new Vector2I(position.X / TileSize, position.Y / TileSize);
    }
    
    private void IdentifyHallwayEdges()
    {
        _hallwayEdges.Clear();
        
        HashSet<Vector2I> allFloorPositions = new HashSet<Vector2I>(_hallwayPositions);
        
        foreach (var pos in _hallwayPositions)
        {
            foreach (var dir in _cardinalDirections)
            {
                Vector2I adjacentPos = pos + dir;
                
                if (!allFloorPositions.Contains(adjacentPos))
                {
                    _hallwayEdges.Add(adjacentPos);
                }
            }
        }
        
        GD.Print($"Identified {_hallwayEdges.Count} hallway edge positions for wall placement");
    }
    
    private void PrecomputeWallPositions(List<Rect2I> rooms)
    {
        _wallPositions.Clear();
        
        // Mark wall positions for main rooms
        foreach (Rect2I room in rooms)
        {
            int widthInTiles = room.Size.X / TileSize;
            int heightInTiles = room.Size.Y / TileSize;
            
            if (widthInTiles < MinRoomWidth || heightInTiles < MinRoomHeight)
                continue;
                
            Vector2I topLeft = GetTilePosition(room.Position);
            Vector2I bottomRight = GetTilePosition(room.Position + room.Size);
            
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
        
        // Add hallway edge walls
        foreach (var pos in _hallwayEdges)
        {
            _wallPositions[pos] = true;
        }
    }
    
    private void PlaceFloorTiles(List<Rect2I> rooms)
    {
        foreach (Rect2I room in rooms)
        {
            Vector2I topLeft = GetTilePosition(room.Position);
            Vector2I bottomRight = GetTilePosition(room.Position + room.Size);
            
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    
                    if (_hallwayEdges.Contains(pos))
                        continue;
                    
                    if (_hallwayPositions.Contains(pos) || !_wallPositions.ContainsKey(pos))
                    {
                        Vector2I atlasCoord = GetFloorTileVariation(pos);
                        _floorLayer.SetCell(pos, 2, atlasCoord, 0);
                        _floorPositions[pos] = true;
                    }
                }
            }
        }
    }
    
    private Vector2I GetFloorTileVariation(Vector2I pos)
    {
        if (_hallwayPositions.Contains(pos))
        {
            return new Vector2I(2, 2);
        }
        
        bool[] wallsAround = new bool[8];
        
        for (int i = 0; i < _directions.Length; i++)
        {
            Vector2I checkPos = pos + _directions[i];
            wallsAround[i] = _wallPositions.ContainsKey(checkPos);
        }
        
        if (CountWallsAround(wallsAround) == 8)
        {
            return new Vector2I(4, 0);
        }
        
        if (wallsAround[N] && wallsAround[W])
        {
            return new Vector2I(1, 1);
        }
        if (wallsAround[N] && !wallsAround[W] && !wallsAround[E])
        {
            return new Vector2I(2, 1);
        }
        if (wallsAround[N] && wallsAround[E])
        {
            return new Vector2I(3, 1);
        }
        if (!wallsAround[N] && !wallsAround[S] && wallsAround[W])
        {
            return new Vector2I(1, 2);
        }
        if (!wallsAround[N] && !wallsAround[S] && wallsAround[E])
        {
            return new Vector2I(3, 2);
        }
        if (wallsAround[W] && wallsAround[S])
        {
            return new Vector2I(1, 3);
        }
        if (wallsAround[S] && !wallsAround[W] && !wallsAround[E])
        {
            return new Vector2I(2, 3);
        }
        if (wallsAround[S] && wallsAround[E])
        {
            return new Vector2I(3, 3);
        }
        
        if (wallsAround[SE])
        {
            return new Vector2I(5, 0);
        }
        if (wallsAround[SW])
        {
            return new Vector2I(6, 0);
        }
        if (wallsAround[NE])
        {
            return new Vector2I(5, 1);
        }
        if (wallsAround[NW])
        {
            return new Vector2I(6, 1);
        }
        
        if (wallsAround[NE] && wallsAround[SE])
        {
            return new Vector2I(5, 2);
        }
        if (wallsAround[NW] && wallsAround[NE])
        {
            return new Vector2I(6, 2);
        }
        if (wallsAround[NW] && wallsAround[SW])
        {
            return new Vector2I(5, 3);
        }
        if (wallsAround[SW] && wallsAround[SE])
        {
            return new Vector2I(6, 3);
        }
        
        if (wallsAround[W] && !wallsAround[E] && !wallsAround[N] && !wallsAround[S])
        {
            return new Vector2I(1, 0);
        }
        if (wallsAround[E] && !wallsAround[W] && !wallsAround[N] && !wallsAround[S])
        {
            return new Vector2I(3, 0);
        }
        
        if (wallsAround[N] && !wallsAround[S])
        {
            return new Vector2I(4, 1);
        }
        if (wallsAround[N] && wallsAround[S])
        {
            return new Vector2I(4, 2);
        }
        if (!wallsAround[N] && wallsAround[S])
        {
            return new Vector2I(4, 3);
        }
        
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
            int widthInTiles = room.Size.X / TileSize;
            int heightInTiles = room.Size.Y / TileSize;
            
            if (widthInTiles < MinRoomWidth || heightInTiles < MinRoomHeight)
                continue;
                
            Vector2I topLeft = GetTilePosition(room.Position);
            Vector2I bottomRight = GetTilePosition(room.Position + room.Size);
            
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    Vector2I pos = new Vector2I(x, y);
                    
                    if (_hallwayPositions.Contains(pos))
                        continue;
                        
                    bool isEdge = x == topLeft.X || x == bottomRight.X - 1 || 
                                 y == topLeft.Y || y == bottomRight.Y - 1;
                    
                    if (isEdge)
                    {
                        RemoveFloorTileAt(pos);
                        
                        // Determine position flags for room wall
                        bool isTop = y == topLeft.Y;
                        bool isBottom = y == bottomRight.Y - 1;
                        bool isLeft = x == topLeft.X;
                        bool isRight = x == bottomRight.X - 1;
                        
                        Vector2I wallType = DetermineWallType(isTop, isBottom, isLeft, isRight);
                        _wallLayer.SetCell(pos, 0, wallType, 0);
                    }
                }
            }
        }
    }
    
    private void RemoveFloorTileAt(Vector2I pos)
    {
        if (_floorPositions.ContainsKey(pos))
        {
            _floorPositions.Remove(pos);
            _floorLayer.EraseCell(pos);
        }
    }
    
    private void PlaceHallwayEdgeWalls()
    {
        foreach (Vector2I pos in _hallwayEdges)
        {
            if (_wallLayer.GetCellSourceId(pos) != -1)
                continue;
                
            RemoveFloorTileAt(pos);
            
            // Check neighboring floor tiles
            bool northFloor = _floorPositions.ContainsKey(pos + new Vector2I(0, -1));
            bool eastFloor = _floorPositions.ContainsKey(pos + new Vector2I(1, 0));
            bool southFloor = _floorPositions.ContainsKey(pos + new Vector2I(0, 1));
            bool westFloor = _floorPositions.ContainsKey(pos + new Vector2I(-1, 0));
            
            Vector2I wallType = DetermineWallType(
                southFloor, northFloor, eastFloor, westFloor);
            
            _wallLayer.SetCell(pos, 0, wallType, 0);
        }
    }
    
    private Vector2I DetermineWallType(bool isTop, bool isBottom, bool isLeft, bool isRight)
    {
        // Corners
        if (isTop && isLeft)
            return new Vector2I(4, 0);     // Bottom right corner
        if (isTop && isRight) 
            return new Vector2I(5, 0);     // Bottom left corner
        if (isBottom && isLeft)
            return new Vector2I(4, 1);     // Top right corner
        if (isBottom && isRight)
            return new Vector2I(5, 1);     // Top left corner
        
        // Straight walls
        if (isTop)
            return new Vector2I(1, 3);     // Bottom edge
        if (isBottom)
            return new Vector2I(1, 1);     // Top edge
        if (isLeft)
            return new Vector2I(2, 2);     // Right edge
        if (isRight)
            return new Vector2I(0, 2);     // Left edge
        
        // Default
        return new Vector2I(1, 2);
    }
    
}
