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
    
    [Export] public TileMapLayer FloorLayer;
    [Export] public TileMapLayer WallLayer;
    
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
    private TilePlacer _tilePlacer;
    
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
        _rng.Randomize();
        
        // Ensure we have valid layers
        if (FloorLayer == null || WallLayer == null)
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
            // Generate immediately without visualization
            GenerateDungeon();
            RenderDungeon();
        }
    }
    
    private void InitializeComponents()
    {
        // Initialize room generator
        _roomGenerator = GetNode<RoomGenerator>("RoomGenerator");
        _roomGenerator.TileSize = TileSize;
        _roomGenerator.NumberOfCells = NumberOfCells;
        _roomGenerator.CellSpawnRadius = CellSpawnRadius;
        
        // Initialize room separator
        _roomSeparator = GetNode<RoomSeparator>("RoomSeparator");
        _roomSeparator.TileSize = TileSize;
        _roomSeparator.PhysicsTimeScale = PhysicsTimeScale; // Set slower physics simulation
        
        // Initialize room determinator
        _roomDeterminator = GetNode<MainRoomDeterminator>("MainRoomDeterminator");
        _roomDeterminator.TileSize = TileSize;
        _roomDeterminator.LargestRoomsPercent = LargestRoomsPercent;
        
        // Initialize graph generator
        _graphGenerator = GetNode<GraphGenerator>("GraphGenerator");
        _graphGenerator.LoopPercent = LoopPercent; // Pass the loop percentage from the main generator
        
        // Initialize hallway generator
        _hallwayGenerator = GetNode<HallwayGenerator>("HallwayGenerator"); 
        _hallwayGenerator.TileSize = TileSize;
        
        // Initialize extra room determinator
        _extraRoomDeterminator = GetNode<ExtraRoomDeterminator>("ExtraRoomDeterminator");
        _extraRoomDeterminator.TileSize = TileSize;
        _extraRoomDeterminator.LargestExtraRoomsPercent = ExtraRoomsPercent;
        
        // Initialize tile placer
        _tilePlacer = GetNode<TilePlacer>("TilePlacer");
        if (_tilePlacer == null)
        {
            // Create a new TilePlacer if it doesn't exist in the scene
            _tilePlacer = new TilePlacer();
            _tilePlacer.Name = "TilePlacer";
            AddChild(_tilePlacer);
        }
        _tilePlacer.TileSize = TileSize;
        _tilePlacer.Initialize(FloorLayer, WallLayer);
        
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
    
    private void GenerateDungeon()
    {
        // Step 1: Generate random cells
        _cells = _roomGenerator.GenerateCells();
        
        // Step 2: Separate cells using iterative method instead of physics when visualization is disabled
        _cells = _roomSeparator.SeparateCellsStep(_cells, 100); // Use step-based separation instead of physics
        
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
        // Make sure TilePlacer is initialized with the current layers
        _tilePlacer.Initialize(FloorLayer, WallLayer);
        
        // Use the TilePlacer to handle all tile placement
        _tilePlacer.PlaceTiles(_rooms);
    }
}