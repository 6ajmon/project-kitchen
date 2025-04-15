using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonGenerator : Node2D
{
    [Export] public int NumberOfCells = 150;
    [Export] public float CellSpawnRadius = 20.0f;
    [Export] public float LargestRoomsPercent = 0.3f;
    [Export] public float LoopPercent = 0.15f;
    [Export] public int TileSize = 16;
    
    [Export] public TileMapLayer FloorLayer;
    [Export] public TileMapLayer WallLayer;
    
    [Export] public bool EnableVisualization = true;
    [Export] public float VisualizationStepDelay = 0.5f;
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private List<Rect2> _cells = new List<Rect2>();
    private List<Rect2> _rooms = new List<Rect2>();
    private List<Vector2> _roomCenters = new List<Vector2>();
    private List<(int, int)> _corridorEdges = new List<(int, int)>();
    
    // Child nodes
    private RoomGenerator _roomGenerator;
    private RoomSeparator _roomSeparator;
    private MainRoomDeterminator _roomDeterminator;
    private GraphGenerator _graphGenerator;
    private HallwayGenerator _hallwayGenerator;
    private GeneratorVisualizer _visualizer;
    
    public enum GenerationState
    {
        Idle,
        GeneratingCells,
        SeparatingCells,
        DeterminingRooms,
        ConnectingRooms,
        CreatingCorridors,
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
        
        // Initialize room determinator (z zaktualizowanymi parametrami)
        _roomDeterminator = GetNode<MainRoomDeterminator>("MainRoomDeterminator");
        _roomDeterminator.TileSize = TileSize;
        _roomDeterminator.LargestRoomsPercent = LargestRoomsPercent;
        
        // Initialize graph generator
        _graphGenerator = GetNode<GraphGenerator>("GraphGenerator");
        _graphGenerator.LoopPercent = LoopPercent;
        
        // Initialize hallway generator
        _hallwayGenerator = GetNode<HallwayGenerator>("HallwayGenerator");
        
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
                    _maxVisualizationSteps = 100; // Number of separation iterations
                    GD.Print("Cells generated. Starting separation...");
                }
                break;
                
            case GenerationState.SeparatingCells:
                if (_currentVisualizationStep < _maxVisualizationSteps)
                {
                    _cells = _roomSeparator.SeparateCellsStep(_cells);
                    _visualizer.VisualizeAllCells(_cells);
                    _currentVisualizationStep++;
                    GD.Print($"Separation: step {_currentVisualizationStep}/{_maxVisualizationSteps}");
                }
                else
                {
                    _currentState = GenerationState.DeterminingRooms;
                    _currentVisualizationStep = 0;
                    GD.Print("Separation complete. Determining rooms...");
                }
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
                    var delaunayEdges = _graphGenerator.GenerateDelaunayTriangulation(_roomCenters);
                    _visualizer.VisualizeDelaunayTriangulation(_roomCenters, delaunayEdges);
                    _currentVisualizationStep++;
                    GD.Print("Delaunay triangulation complete. Creating MST...");
                }
                else if (_currentVisualizationStep == 1)
                {
                    // MST
                    var mstEdges = _graphGenerator.GenerateMinimalSpanningTree(_roomCenters);
                    _visualizer.VisualizeMinimalSpanningTree(_roomCenters, mstEdges);
                    _currentVisualizationStep++;
                    GD.Print("MST created. Adding loops...");
                }
                else if (_currentVisualizationStep == 2)
                {
                    // Additional loops
                    _corridorEdges = _graphGenerator.AddLoops();
                    _visualizer.VisualizeLoops(_roomCenters, _corridorEdges);
                    _currentState = GenerationState.CreatingCorridors;
                    _currentVisualizationStep = 0;
                    GD.Print("Additional connections added. Creating corridors...");
                }
                break;
                
            case GenerationState.CreatingCorridors:
                if (_currentVisualizationStep == 0)
                {
                    _rooms = _hallwayGenerator.CreateCorridors(_cells, _rooms, _roomCenters, _corridorEdges);
                    _visualizer.VisualizeCorridors(_cells, _rooms, _roomCenters);
                    _currentState = GenerationState.RenderingDungeon;
                    _currentVisualizationStep = 0;
                    GD.Print("Corridors created. Rendering dungeon...");
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
    
    private void GenerateDungeon()
    {
        // Step 1: Generate random cells
        _cells = _roomGenerator.GenerateCells();
        
        // Step 2: Separate cells using steering behavior
        _cells = _roomSeparator.SeparateCells(_cells, 100); // Run 100 iterations of separation
        
        // Step 3: Determine which cells are rooms
        (_rooms, _roomCenters) = _roomDeterminator.DetermineRooms(_cells);
        
        // Step 4: Connect rooms using Delaunay, MST, and adding loops
        _graphGenerator.GenerateDelaunayTriangulation(_roomCenters);
        _graphGenerator.GenerateMinimalSpanningTree(_roomCenters);
        _corridorEdges = _graphGenerator.AddLoops();
        
        // Step 5: Create corridors between connected rooms
        _rooms = _hallwayGenerator.CreateCorridors(_cells, _rooms, _roomCenters, _corridorEdges);
    }
    
    private void RenderDungeon()
    {
        // Clear existing tiles
        FloorLayer.Clear();
        WallLayer.Clear();
        
        // First place floor tiles for rooms and corridors
        foreach (Rect2 room in _rooms)
        {
            // Convert room coordinates to tile coordinates
            Vector2I topLeft = new Vector2I(
                Mathf.FloorToInt(room.Position.X / TileSize),
                Mathf.FloorToInt(room.Position.Y / TileSize)
            );
            
            Vector2I bottomRight = new Vector2I(
                Mathf.CeilToInt(room.End.X / TileSize),
                Mathf.CeilToInt(room.End.Y / TileSize)
            );
            
            // Place floor tiles
            for (int x = topLeft.X; x < bottomRight.X; x++)
            {
                for (int y = topLeft.Y; y < bottomRight.Y; y++)
                {
                    FloorLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0), 0);
                }
            }
        }
        
        // Then place wall tiles around floor tiles
        int searchRadius = (int)(CellSpawnRadius * 2);
        for (int x = -searchRadius; x < searchRadius; x++)
        {
            for (int y = -searchRadius; y < searchRadius; y++)
            {
                Vector2I pos = new Vector2I(x, y);
                
                // If this position doesn't have a floor
                if (FloorLayer.GetCellSourceId(pos) == -1)
                {
                    // Check if any adjacent cell has a floor
                    bool adjacentToFloor = false;
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        for (int dy = -1; dy <= 1; dy++)
                        {
                            if (dx == 0 && dy == 0) continue;
                            
                            Vector2I checkPos = pos + new Vector2I(dx, dy);
                            if (FloorLayer.GetCellSourceId(checkPos) != -1)
                            {
                                adjacentToFloor = true;
                                break;
                            }
                        }
                        if (adjacentToFloor) break;
                    }
                    
                    // If adjacent to floor, place a wall
                    if (adjacentToFloor)
                    {
                        WallLayer.SetCell(pos, 1, new Vector2I(0, 0), 0);
                    }
                }
            }
        }
    }
}