using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonGenerator : Node2D
{
    [Export] public int NumberOfCells = 150;
    [Export] public float CellSpawnRadius = 20.0f;
    [Export] public float LargestRoomsPercent = 0.3f;
    [Export] public float LoopPercent = 0.1f;
    [Export] public int TileSize = 16;
    [Export] public float ExtraRoomsPercent = 1.0f;
    [Export] public int Seed = 0; // Add seed parameter, 0 means generate random seed
    
    [Export] public TileMapLayer WorldTileMap;
    [Export] public TileMapLayer DisplayTileMap;
    
    [Export] public bool EnableVisualization = true;
    [Export] public float VisualizationStepDelay = 0.5f;
    [Export] public float PhysicsTimeScale = 0.1f; // Slower physics simulation for better visualization
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private List<Rect2I> _cells = new List<Rect2I>();
    private List<Rect2I> _rooms = new List<Rect2I>();
    private List<Vector2I> _roomCenters = new List<Vector2I>();
    private List<(int, int)> _corridorEdges = new List<(int, int)>();
    private List<Rect2I> _extraRooms = new List<Rect2I>();
    private List<Vector2I> _extraRoomCenters = new List<Vector2I>();
    
    // Child nodes
    private RoomGenerator _roomGenerator;
    private RoomSeparator _roomSeparator;
    private MainRoomDeterminator _roomDeterminator;
    private GraphGenerator _graphGenerator;
    private HallwayGenerator _hallwayGenerator;
    private GeneratorVisualizer _visualizer;
    private ExtraRoomDeterminator _extraRoomDeterminator;
    
    public enum GenerationState
    {
        Idle,
        GeneratingCells,
        SeparatingCells,
        DeterminingRooms,
        ConnectingRooms,
        CreatingCorridors,
        FindingExtraRooms,
        RenderingDungeon,
        Complete
    }
    
    private GenerationState _currentState = GenerationState.Idle;
    private int _currentVisualizationStep = 0;
    private int _maxVisualizationSteps = 0;
    private float _visualizationTimer = 0;
    
    public override void _Ready()
    {
        // Initialize RNG with seed
        if (Seed == 0)
        {
            // Generate a random seed if none provided
            Seed = (int)DateTime.Now.Ticks;
        }
        
        _rng.Seed = (ulong)Seed;
        GD.Print($"Using seed: {Seed}");
        
        // Ensure we have valid layers
        if (WorldTileMap == null || DisplayTileMap == null)
        {
            GD.PrintErr("Floor or Wall layer not assigned. Please assign them in the Inspector.");
            return;
        }
        
        // Initialize child components
        InitializeComponents();
        
        if (EnableVisualization)
        {
            // Start generation with visualization
            StartGenerationSequence();
        }
        else
        {
            // Start non-visualized generation but still use async approach
            StartNonVisualizedGeneration();
        }
    }
    
    private void InitializeComponents()
    {
        // Initialize room generator
        _roomGenerator = GetNode<RoomGenerator>("RoomGenerator");
        _roomGenerator.TileSize = TileSize;
        _roomGenerator.NumberOfCells = NumberOfCells;
        _roomGenerator.CellSpawnRadius = CellSpawnRadius;
        _roomGenerator.SetSeed(Seed); // Pass the seed
        
        // Initialize room separator
        _roomSeparator = GetNode<RoomSeparator>("RoomSeparator");
        _roomSeparator.TileSize = TileSize;
        _roomSeparator.PhysicsTimeScale = PhysicsTimeScale; // Set slower physics simulation
        _roomSeparator.SetSeed(Seed); // Pass the seed
        
        // Initialize room determinator
        _roomDeterminator = GetNode<MainRoomDeterminator>("MainRoomDeterminator");
        _roomDeterminator.TileSize = TileSize;
        _roomDeterminator.LargestRoomsPercent = LargestRoomsPercent;
        
        // Initialize graph generator
        _graphGenerator = GetNode<GraphGenerator>("GraphGenerator");
        _graphGenerator.LoopPercent = LoopPercent; // Pass the loop percentage from the main generator
        _graphGenerator.SetSeed(Seed); // Pass the seed
        
        // Initialize hallway generator
        _hallwayGenerator = GetNode<HallwayGenerator>("HallwayGenerator"); 
        _hallwayGenerator.TileSize = TileSize;
        _hallwayGenerator.SetSeed(Seed); // Pass the seed
        
        // Initialize extra room determinator
        _extraRoomDeterminator = GetNode<ExtraRoomDeterminator>("ExtraRoomDeterminator");
        _extraRoomDeterminator.TileSize = TileSize;
        _extraRoomDeterminator.LargestExtraRoomsPercent = ExtraRoomsPercent;
    
        // Initialize visualizer if needed
        if (EnableVisualization)
        {
            _visualizer = GetNode<GeneratorVisualizer>("GeneratorVisualizer");
            _visualizer.TileSize = TileSize;
        }
    }
    
    public override void _Process(double delta)
    {
        if (!EnableVisualization || _currentState == GenerationState.Idle || 
            _currentState == GenerationState.Complete)
            return;
            
        _visualizationTimer += (float)delta;
        
        if (_visualizationTimer >= VisualizationStepDelay)
        {
            _visualizationTimer = 0;
            ProcessNextVisualizationStep();
        }
    }
    
    private void StartGenerationSequence()
    {
        _currentState = GenerationState.GeneratingCells;
        _currentVisualizationStep = 0;
        GD.Print("Starting cell generation...");
    }
    
    private void ProcessNextVisualizationStep()
    {
        switch (_currentState)
        {
            case GenerationState.GeneratingCells:
                if (_currentVisualizationStep == 0)
                {
                    _cells = _roomGenerator.GenerateCells();
                    _visualizer.VisualizeAllCells(_cells);
                    _currentState = GenerationState.SeparatingCells;
                    _currentVisualizationStep = 0;
                    
                    // Use physics-based separation
                    var task = _roomSeparator.SeparateCellsWithPhysics(_cells);
                    task.ContinueWith(t => 
                    {
                        _cells = t.Result;
                        // Signal that separation is complete
                        CallDeferred("_OnSeparationComplete");
                    });
                    
                    GD.Print("Cells generated. Starting physics-based separation...");
                }
                break;
                
            case GenerationState.SeparatingCells:
                // No iteration needed - physics engine is handling it
                // Update visualization every frame for smooth animation
                _visualizer.VisualizeAllCells(_roomSeparator.GetCurrentPositions());
                _currentVisualizationStep++;
                break;
                
            case GenerationState.DeterminingRooms:
                if (_currentVisualizationStep == 0)
                {
                    (_rooms, _roomCenters) = _roomDeterminator.DetermineRooms(_cells);
                    _visualizer.VisualizeRooms(_rooms, _roomCenters);
                    _currentState = GenerationState.ConnectingRooms;
                    _currentVisualizationStep = 0;
                    GD.Print("Rooms determined. Starting Delaunay triangulation...");
                }
                break;
                
            case GenerationState.ConnectingRooms:
                if (_currentVisualizationStep == 0)
                {
                    // Delaunay triangulation
                    if (_roomCenters.Count < 2)
                    {
                        GD.PrintErr("Not enough room centers for triangulation. Need at least 2 rooms.");
                        _currentState = GenerationState.RenderingDungeon;
                        break;
                    }
                    
                    var delaunayEdges = _graphGenerator.GenerateDelaunayTriangulation(_roomCenters);
                    if (delaunayEdges.Count == 0)
                    {
                        GD.PrintErr("Delaunay triangulation returned no edges.");
                    }
                    else
                    {
                        GD.Print($"Generated {delaunayEdges.Count} Delaunay edges for {_roomCenters.Count} rooms");
                    }
                    
                    _visualizer.VisualizeDelaunayTriangulation(_roomCenters, delaunayEdges);
                    _currentVisualizationStep++;
                    GD.Print("Delaunay triangulation complete. Creating MST...");
                }
                else if (_currentVisualizationStep == 1)
                {
                    // MST
                    var mstEdges = _graphGenerator.GenerateMinimalSpanningTree(_roomCenters);
                    if (mstEdges.Count == 0)
                    {
                        GD.PrintErr("MST generation returned no edges.");
                    }
                    
                    _visualizer.VisualizeMinimalSpanningTree(_roomCenters, mstEdges);
                    _currentVisualizationStep++;
                    GD.Print("MST created. Adding loops...");
                }
                else if (_currentVisualizationStep == 2)
                {
                    // Additional loops
                    _corridorEdges = _graphGenerator.AddLoops();
                    
                    // Ensure we have at least the MST edges
                    if (_corridorEdges.Count == 0)
                    {
                        GD.PrintErr("No corridor edges generated. Using MST as fallback.");
                        _corridorEdges = _graphGenerator.GetMSTEdges();
                    }
                    
                    _visualizer.VisualizeLoops(_roomCenters, _corridorEdges);
                    _currentState = GenerationState.CreatingCorridors;
                    _currentVisualizationStep = 0;
                    GD.Print($"Added loops. Total corridor edges: {_corridorEdges.Count}");
                }
                break;
                
            case GenerationState.CreatingCorridors:
                if (_currentVisualizationStep == 0)
                {
                    _rooms = _hallwayGenerator.CreateCorridors(_cells, _rooms, _roomCenters, _corridorEdges);
                    _visualizer.VisualizeCorridors(_cells, _rooms, _roomCenters);
                    _currentState = GenerationState.FindingExtraRooms;
                    _currentVisualizationStep = 0;
                    GD.Print("Corridors created. Finding potential extra rooms...");
                }
                break;
                
            case GenerationState.FindingExtraRooms:
                if (_currentVisualizationStep == 0)
                {
                    (_extraRooms, _extraRoomCenters) = _extraRoomDeterminator.DetermineExtraRooms(_cells, _rooms);
                    _visualizer.VisualizeExtraRooms(_extraRooms, _extraRoomCenters);
                    _currentState = GenerationState.RenderingDungeon;
                    _currentVisualizationStep = 0;
                    GD.Print($"Found {_extraRooms.Count} potential extra rooms. Rendering dungeon...");
                }
                break;
                
            case GenerationState.RenderingDungeon:
                if (_currentVisualizationStep == 0)
                {
                    RenderDungeon();
                    _currentState = GenerationState.Complete;
                    GD.Print("Dungeon generation complete!");
                }
                break;
        }
    }
    
    // Called when physics-based separation is complete
    private void _OnSeparationComplete()
    {
        _visualizer.VisualizeAllCells(_cells);
        _currentState = GenerationState.DeterminingRooms;
        _currentVisualizationStep = 0;
        GD.Print("Physics-based separation complete. Determining rooms...");
    }
    
    private void StartNonVisualizedGeneration()
    {
        _currentState = GenerationState.GeneratingCells;
        _cells = _roomGenerator.GenerateCells();
        
        // Use physics-based separation but don't block the main thread
        var task = _roomSeparator.SeparateCellsWithPhysics(_cells);
        task.ContinueWith(t => 
        {
            _cells = t.Result;
            // Signal that generation should continue
            CallDeferred("_CompleteNonVisualizedGeneration");
        });
        
        GD.Print("Started non-visualized dungeon generation...");
    }

    private void _CompleteNonVisualizedGeneration()
    {
        // Step 3: Determine which cells are rooms
        (_rooms, _roomCenters) = _roomDeterminator.DetermineRooms(_cells);
        
        if (_roomCenters.Count < 2)
        {
            GD.PrintErr("Not enough rooms generated. Dungeon generation cannot continue.");
            return;
        }
        
        // Step 4: Connect rooms using Delaunay, MST, and adding loops
        var delaunayEdges = _graphGenerator.GenerateDelaunayTriangulation(_roomCenters);
        var mstEdges = _graphGenerator.GenerateMinimalSpanningTree(_roomCenters);
        _corridorEdges = _graphGenerator.AddLoops();
        
        // Ensure we have at least the MST edges
        if (_corridorEdges.Count == 0)
        {
            GD.PrintErr("No corridor edges generated. Using MST as fallback.");
            _corridorEdges = mstEdges;
        }
        
        // Step 5: Create corridors between connected rooms
        _rooms = _hallwayGenerator.CreateCorridors(_cells, _rooms, _roomCenters, _corridorEdges);
        
        // Step 6: Find potential extra rooms that can be added later
        (_extraRooms, _extraRoomCenters) = _extraRoomDeterminator.DetermineExtraRooms(_cells, _rooms);
        
        // Final step: Render the dungeon
        RenderDungeon();
        _currentState = GenerationState.Complete;
        
        GD.Print("Non-visualized dungeon generation complete!");
    }
    
    // Method to add an extra room to the dungeon during gameplay
    public bool AddExtraRoom(int extraRoomIndex)
    {
        if (extraRoomIndex < 0 || extraRoomIndex >= _extraRooms.Count)
        {
            GD.PrintErr($"Invalid extra room index: {extraRoomIndex}");
            return false;
        }
        
        // Get the room to add
        Rect2I roomToAdd = _extraRooms[extraRoomIndex];
        Vector2I centerToAdd = _extraRoomCenters[extraRoomIndex];
        
        // Find closest dungeon room to connect to
        int closestRoomIndex = FindClosestRoom(centerToAdd);
        if (closestRoomIndex < 0)
        {
            GD.PrintErr("Couldn't find a valid room to connect to");
            return false;
        }
        
        // Add to main rooms
        int newRoomIndex = _rooms.Count;
        _rooms.Add(roomToAdd);
        _roomCenters.Add(centerToAdd);
        
        // Create a corridor from the extra room to the closest dungeon room
        List<(int, int)> newConnection = new List<(int, int)> { (newRoomIndex, closestRoomIndex) };
        _rooms = _hallwayGenerator.CreateCorridors(_cells, _rooms, _roomCenters, newConnection);
        
        // Remove from extra rooms list
        _extraRooms.RemoveAt(extraRoomIndex);
        _extraRoomCenters.RemoveAt(extraRoomIndex);
        
        // Update dungeon rendering using the tile placer
        RenderDungeon();
        
        GD.Print($"Added extra room to the dungeon, connected to room {closestRoomIndex}");
        return true;
    }
    
    private int FindClosestRoom(Vector2I point)
    {
        if (_roomCenters.Count == 0)
            return -1;
            
        int closestIndex = 0;
        float minDistance = float.MaxValue;
        
        for (int i = 0; i < _roomCenters.Count; i++)
        {
            float distance = (_roomCenters[i] - point).LengthSquared();
            if (distance < minDistance)
            {
                minDistance = distance;
                closestIndex = i;
            }
        }
        
        return closestIndex;
    }
    
    // Returns the number of available extra rooms
    public int GetExtraRoomCount()
    {
        return _extraRooms.Count;
    }
    
    // Get extra room information for UI or gameplay logic
    public List<(Rect2I room, Vector2I center)> GetExtraRooms()
    {
        List<(Rect2I, Vector2I)> result = new List<(Rect2I, Vector2I)>();
        for (int i = 0; i < _extraRooms.Count; i++)
        {
            result.Add((_extraRooms[i], _extraRoomCenters[i]));
        }
        return result;
    }
    
    private void RenderDungeon()
    {
        // Clear any existing tiles on the WorldTileMap
        WorldTileMap.Clear();
        
        // Vector2I constants for tile atlas coordinates
        Vector2I wallTile = new Vector2I(0, 0);
        Vector2I floorTile = new Vector2I(1, 0);
        
        // Create a set of all floor tile positions (in tile coordinates, not pixels)
        HashSet<Vector2I> floorPositions = new HashSet<Vector2I>();
        HashSet<Vector2I> wallPositions = new HashSet<Vector2I>();
        
        // NEW: Track corridor positions to detect where hallways meet rooms
        HashSet<Vector2I> corridorPositions = new HashSet<Vector2I>();
        
        // First pass: identify main rooms and corridor cells
        List<Rect2I> mainRooms = new List<Rect2I>();
        List<Rect2I> corridorCells = new List<Rect2I>();
        
        foreach (var room in _rooms)
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
        Rect2I boundingBox = CalculateDungeonBoundingBox(_cells);
        
        // Second pass: add all room and corridor tiles to the floor positions
        foreach (var room in _rooms)
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
                        corridorPositions.Add(tilePos);
                    }
                }
            }
            
            // Only place walls around main rooms, not corridor cells
            if (!IsCorridorCell(room))
            {
                PlaceWallsAroundRoom(startTileX, startTileY, widthInTiles, heightInTiles, floorPositions, wallPositions);
            }
        }
        
        // NEW: Fill empty tiles with walls in the bounding box
        FillEmptySpacesWithWalls(boundingBox, floorPositions, wallPositions);
        
        // NEW: Find walls that should be corridor openings
        List<Vector2I> wallsToRemove = new List<Vector2I>();
        HashSet<Vector2I> openingsToAdd = new HashSet<Vector2I>();
        
        foreach (var wallPos in wallPositions)
        {
            if (ShouldBeCorridorOpening(wallPos, floorPositions, corridorPositions))
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
            WorldTileMap.SetCell(tilePos, 0, floorTile);
        }
        
        // Place wall tiles at all wall positions
        foreach (var wallPos in wallPositions)
        {
            WorldTileMap.SetCell(wallPos, 0, wallTile);
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
                    WorldTileMap.SetCell(neighborTilePos, 0, wallTile);
                    wallPositions.Add(neighborTilePos);
                }
            }
        }
        
        // Update display tiles using the TilePlacer
        GetNode<TilePlacer>("TilePlacer").UpdateDisplayTiles(WorldTileMap, DisplayTileMap);
    }
    
    // Add new helper methods to calculate bounding box and fill empty spaces
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

    // Identify if a cell is a corridor cell (small cell)
    private bool IsCorridorCell(Rect2I cell)
    {
        // Corridor cells are typically small, single-tile cells
        // Use the same criteria as in GeneratorVisualizer.IsCorridorCell
        int minWidthInPixels = 6 * TileSize;
        int minHeightInPixels = 6 * TileSize;
        
        // Check if this is a corridor cell (small) vs a main room (large)
        bool isSmallCell = cell.Size.X < minWidthInPixels || cell.Size.Y < minHeightInPixels;
        
        // Also check if this was one of the original main rooms
        bool isOriginalRoom = false;
        foreach (var originalRoomCenter in _roomCenters)
        {
            // Check if this center falls within the cell
            if (cell.HasPoint(originalRoomCenter))
            {
                isOriginalRoom = true;
                break;
            }
        }
        
        return isSmallCell && !isOriginalRoom;
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

    // Add this new helper method to detect corridor openings
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