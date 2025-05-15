using Godot;
using System;
using System.Collections.Generic;

public partial class DungeonGenerator : Node2D
{
    [ExportGroup("Cell Generation")]
    [Export(PropertyHint.None, "Random seed for dungeon generation (0 = random)")]
    public int Seed = 0;
    
    [Export(PropertyHint.None, "Total number of cells to generate")]
    public int NumberOfCells = 64;
    
    [Export(PropertyHint.None, "Radius for cell generation when using circular distribution")]
    public float CellSpawnRadius = 20.0f;
    
    [Export(PropertyHint.None, "X-axis radius for elliptical cell distribution")]
    public float CellSpawnRadiusX = 60.0f;
    
    [Export(PropertyHint.None, "Y-axis radius for elliptical cell distribution")]
    public float CellSpawnRadiusY = 10.0f;
    
    [ExportGroup("Room Determination")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float LargestRoomsPercent = 0.25f; // What percentage of largest cells become rooms
    
    [ExportGroup("Corridors & Extra Rooms")]
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float LoopPercent = 0.25f; // Percentage of non-MST edges to add as loops
    
    [Export(PropertyHint.Range, "0,1,0.01")]
    public float ExtraRoomsPercent = 1.0f; // What percentage of neighboring cells become extra rooms
    
    [Export(PropertyHint.Range, "1,10,1")]
    public int HallwayWidth = 2; // Width of hallways in tiles
    
    [Export(PropertyHint.Range, "1,10,1")]
    public int NeighborDistance = 2; // Number of tiles to consider as "neighboring" for extra rooms
    
    [ExportGroup("Tile Settings")]
    [Export(PropertyHint.None, "Size of each tile in pixels")]
    public int TileSize = 16;
    
    [Export(PropertyHint.None, "Atlas coordinates for floor tiles")]
    public Vector2I FloorAtlasCoord = new Vector2I(1, 0);
    
    [Export(PropertyHint.None, "Atlas coordinates for wall tiles")]
    public Vector2I WallAtlasCoord = new Vector2I(0, 0);
    
    [ExportGroup("Tilemaps")]
    [Export(PropertyHint.None, "The tilemap for collision/gameplay")]
    public TileMapLayer WorldTileMap;
    
    [Export(PropertyHint.None, "The tilemap for visual display")]
    public TileMapLayer DisplayTileMap;
    
    [ExportGroup("Visualization")]
    [Export(PropertyHint.None, "Whether to visualize the generation process")]
    public bool EnableVisualization = true;
    
    [Export(PropertyHint.Range, "0.1,2.0,0.1")]
    public float VisualizationStepDelay = 0.5f; // Delay between visualization steps
    
    [Export(PropertyHint.Range, "0.001,0.5,0.001")]
    public float PhysicsTimeScale = 0.02f; // Physics simulation speed (lower = slower)
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private List<Rect2I> _cells = new List<Rect2I>();
    private List<Rect2I> _rooms = new List<Rect2I>();
    private List<Vector2I> _roomCenters = new List<Vector2I>();
    private List<(int, int)> _corridorEdges = new List<(int, int)>();
    private List<Rect2I> _extraRooms = new List<Rect2I>();
    private List<Vector2I> _extraRoomCenters = new List<Vector2I>();
    private int _startingRoomIndex = -1;
    
    // Child nodes
    private RoomGenerator _roomGenerator;
    private RoomSeparator _roomSeparator;
    private MainRoomDeterminator _roomDeterminator;
    private GraphGenerator _graphGenerator;
    private HallwayGenerator _hallwayGenerator;
    private GeneratorVisualizer _visualizer;
    private ExtraRoomDeterminator _extraRoomDeterminator;
    private DungeonRenderer _dungeonRenderer;
    
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

    // Add signal for dungeon generation completion
    [Signal]
    public delegate void DungeonGenerationCompletedEventHandler(Vector2I startRoomPosition);
    
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
        _roomGenerator.CellSpawnRadiusX = CellSpawnRadiusX;
        _roomGenerator.CellSpawnRadiusY = CellSpawnRadiusY;
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
        _hallwayGenerator.HallwayWidth = HallwayWidth;
        _hallwayGenerator.SetSeed(Seed); // Pass the seed
        
        // Initialize extra room determinator
        _extraRoomDeterminator = GetNode<ExtraRoomDeterminator>("ExtraRoomDeterminator");
        _extraRoomDeterminator.TileSize = TileSize;
        _extraRoomDeterminator.LargestExtraRoomsPercent = ExtraRoomsPercent;
        _extraRoomDeterminator.NeighborDistance = NeighborDistance;
    
        // Initialize dungeonRenderer
        _dungeonRenderer = GetNode<DungeonRenderer>("DungeonRenderer");
        _dungeonRenderer.TileSize = TileSize;
        _dungeonRenderer.FloorAtlasCoord = FloorAtlasCoord;
        _dungeonRenderer.WallAtlasCoord = WallAtlasCoord;
        _dungeonRenderer.WorldTileMap = WorldTileMap;
        _dungeonRenderer.DisplayTileMap = DisplayTileMap;
        
        // Initialize visualizer if needed
        if (EnableVisualization)
        {
            _visualizer = GetNode<GeneratorVisualizer>("GeneratorVisualizer");
            _visualizer.TileSize = TileSize;
        }
        
        // Initialize tile placer
        TilePlacer tilePlacer = GetNode<TilePlacer>("TilePlacer");
        if (tilePlacer != null)
        {
            tilePlacer.FloorAtlasCoord = FloorAtlasCoord;
            tilePlacer.WallAtlasCoord = WallAtlasCoord;
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
                    
                    // Determine starting room
                    _startingRoomIndex = _roomDeterminator.DetermineStartingRoom(_roomCenters);
                    
                    _visualizer.VisualizeRooms(_rooms, _roomCenters, _startingRoomIndex);
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
        
        // Determine starting room
        _startingRoomIndex = _roomDeterminator.DetermineStartingRoom(_roomCenters);
        
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
    
    
    private void RenderDungeon()
    {
        // Use the DungeonRenderer to render the dungeon with starting room information
        _dungeonRenderer.RenderDungeon(_cells, _rooms, _roomCenters, new List<Vector2I>(), _startingRoomIndex);
        
        // Emit signal that dungeon generation is complete with starting room position
        if (_startingRoomIndex >= 0 && _startingRoomIndex < _roomCenters.Count)
        {
            EmitSignal(SignalName.DungeonGenerationCompleted, _roomCenters[_startingRoomIndex]);
            GD.Print($"Dungeon generation complete. Starting room position: {_roomCenters[_startingRoomIndex]}");
        }
        else
        {
            GD.PrintErr("Invalid starting room index. Cannot emit completion signal.");
        }
    }
}