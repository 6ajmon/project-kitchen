using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DungeonGenerator : Node2D
{
    [Export] public int NumberOfCells = 150;
    [Export] public float CellSpawnRadius = 20.0f; // Zmniejszona wartość, komórki będą bliżej siebie
    [Export] public float MinRoomWidth = 6; // Minimalna szerokość w kaflach
    [Export] public float MinRoomHeight = 6; // Minimalna wysokość w kaflach
    [Export] public float LoopPercent = 0.15f; // Percentage of edges to re-add
    [Export] public int TileSize = 128; // Rozmiar kafla w pikselach
    
    [Export] public TileMapLayer FloorLayer;
    [Export] public TileMapLayer WallLayer;
    
    // Parametry dla wizualizacji
    [Export] public bool EnableVisualization = true;
    [Export] public float VisualizationStepDelay = 0.5f; // Opóźnienie między krokami w sekundach
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    private List<Rect2> _cells = new List<Rect2>();
    private List<Rect2> _rooms = new List<Rect2>();
    private List<Vector2> _roomCenters = new List<Vector2>();
    private List<(int, int)> _corridorEdges = new List<(int, int)>();
    
    // Kontenery dla wizualizacji
    private Node _visualizationContainer;
    private Node _cellsVisContainer;
    private Node _roomsVisContainer;
    private Node _delaunayVisContainer;
    private Node _mstVisContainer;
    private Node _loopsVisContainer;
    private Node _corridorsVisContainer;
    
    private enum VisualizationState
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
    
    private VisualizationState _currentState = VisualizationState.Idle;
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
        
        // Inicjalizacja kontenerów wizualizacji
        if (EnableVisualization)
        {
            InitializeVisualizationContainers();
        }
        
        if (EnableVisualization)
        {
            // Rozpocznij proces generowania z wizualizacją
            StartGenerationSequence();
        }
        else
        {
            // Generuj natychmiastowo bez wizualizacji
            GenerateDungeon();
            RenderDungeon();
        }
    }
    
    public override void _Process(double delta)
    {
        if (!EnableVisualization || _currentState == VisualizationState.Idle || 
            _currentState == VisualizationState.Complete)
            return;
            
        _visualizationTimer += (float)delta;
        
        if (_visualizationTimer >= VisualizationStepDelay)
        {
            _visualizationTimer = 0;
            ProcessNextVisualizationStep();
        }
    }
    
    private void InitializeVisualizationContainers()
    {
        _visualizationContainer = new Node2D();
        _visualizationContainer.Name = "VisualizationContainer";
        AddChild(_visualizationContainer);
        
        _cellsVisContainer = new Node2D();
        _cellsVisContainer.Name = "CellsContainer";
        _visualizationContainer.AddChild(_cellsVisContainer);
        
        _roomsVisContainer = new Node2D();
        _roomsVisContainer.Name = "RoomsContainer";
        _visualizationContainer.AddChild(_roomsVisContainer);
        
        _delaunayVisContainer = new Node2D();
        _delaunayVisContainer.Name = "DelaunayContainer";
        _visualizationContainer.AddChild(_delaunayVisContainer);
        
        _mstVisContainer = new Node2D();
        _mstVisContainer.Name = "MSTContainer";
        _visualizationContainer.AddChild(_mstVisContainer);
        
        _loopsVisContainer = new Node2D();
        _loopsVisContainer.Name = "LoopsContainer";
        _visualizationContainer.AddChild(_loopsVisContainer);
        
        _corridorsVisContainer = new Node2D();
        _corridorsVisContainer.Name = "CorridorsContainer";
        _visualizationContainer.AddChild(_corridorsVisContainer);
    }
    
    private void StartGenerationSequence()
    {
        _currentState = VisualizationState.GeneratingCells;
        _currentVisualizationStep = 0;
        GD.Print("Rozpoczynam generowanie komórek...");
    }
    
    private void ProcessNextVisualizationStep()
    {
        switch (_currentState)
        {
            case VisualizationState.GeneratingCells:
                if (_currentVisualizationStep == 0)
                {
                    GenerateCells();
                    VisualizeAllCells();
                    _currentState = VisualizationState.SeparatingCells;
                    _currentVisualizationStep = 0;
                    _maxVisualizationSteps = 100; // Liczba iteracji separacji
                    GD.Print("Komórki wygenerowane. Rozpoczynam separację...");
                }
                break;
                
            case VisualizationState.SeparatingCells:
                if (_currentVisualizationStep < _maxVisualizationSteps)
                {
                    SeparateCellsStep();
                    VisualizeAllCells();
                    _currentVisualizationStep++;
                    GD.Print($"Separacja: krok {_currentVisualizationStep}/{_maxVisualizationSteps}");
                }
                else
                {
                    _currentState = VisualizationState.DeterminingRooms;
                    _currentVisualizationStep = 0;
                    GD.Print("Separacja zakończona. Określam pokoje...");
                }
                break;
                
            case VisualizationState.DeterminingRooms:
                if (_currentVisualizationStep == 0)
                {
                    DetermineRooms();
                    VisualizeRooms();
                    _currentState = VisualizationState.ConnectingRooms;
                    _currentVisualizationStep = 0;
                    GD.Print("Pokoje określone. Rozpoczynam triangulację Delaunay...");
                }
                break;
                
            case VisualizationState.ConnectingRooms:
                if (_currentVisualizationStep == 0)
                {
                    // Triangulacja Delaunay i MST w jednym kroku
                    ConnectRooms();
                    // Wizualizacja triangulacji Delaunay
                    VisualizeDelaunayTriangulation();
                    _currentVisualizationStep++;
                    GD.Print("Triangulacja Delaunay zakończona. Tworzę MST...");
                }
                else if (_currentVisualizationStep == 1)
                {
                    // MST
                    VisualizeMinimalSpanningTree();
                    _currentVisualizationStep++;
                    GD.Print("MST utworzone. Dodaję dodatkowe połączenia...");
                }
                else if (_currentVisualizationStep == 2)
                {
                    // Dodatkowe pętle
                    VisualizeLoops();
                    _currentVisualizationStep = 0;
                    _currentState = VisualizationState.CreatingCorridors;
                    GD.Print("Dodatkowe połączenia dodane. Tworzę korytarze...");
                }
                break;
                
            case VisualizationState.CreatingCorridors:
                if (_currentVisualizationStep == 0)
                {
                    CreateCorridors();
                    VisualizeCorridors();
                    _currentState = VisualizationState.RenderingDungeon;
                    _currentVisualizationStep = 0;
                    GD.Print("Korytarze utworzone. Renderuję loch...");
                }
                break;
                
            case VisualizationState.RenderingDungeon:
                if (_currentVisualizationStep == 0)
                {
                    RenderDungeon();
                    _currentState = VisualizationState.Complete;
                    GD.Print("Generowanie lochu zakończone!");
                }
                break;
        }
    }
    
    private void GenerateDungeon()
    {
        // Step 1 & 2: Generate random cells
        GenerateCells();
        
        // Step 3: Separate cells using steering behavior
        SeparateCells(100); // Run 100 iterations of separation
        
        // Step 5: Determine which cells are rooms
        DetermineRooms();
        
        // Step 6-8: Connect rooms using Delaunay, MST, and adding loops
        ConnectRooms();
        
        // Step 9: Create corridors between connected rooms
        CreateCorridors();
    }
    
    private void GenerateCells()
    {
        _cells.Clear();
        
        for (int i = 0; i < NumberOfCells; i++)
        {
            // Generujemy rozmiary w jednostkach kafelków (tiles)
            float widthInTiles = NormalRandom(2, 12);  // Od 3 do 8 kafelków szerokości
            float heightInTiles = NormalRandom(2, 12); // Od 3 do 8 kafelków wysokości
            
            // Ensure width/height ratio is reasonable
            // if (widthInTiles / heightInTiles > 2.5f) heightInTiles = widthInTiles / 2.0f;
            // if (heightInTiles / widthInTiles > 2.5f) widthInTiles = heightInTiles / 2.0f;
            
            // Konwertujemy rozmiary na piksele
            float width = widthInTiles * TileSize;
            float height = heightInTiles * TileSize;
            
            // Generate random position within radius (również w jednostkach kafelków)
            float angle = _rng.RandfRange(0, Mathf.Pi * 2);
            float distance = _rng.RandfRange(0, CellSpawnRadius);
            Vector2 position = new Vector2(
                Mathf.Cos(angle) * distance * TileSize,
                Mathf.Sin(angle) * distance * TileSize
            );
            
            _cells.Add(new Rect2(position.X, position.Y, width, height));
        }
    }
    
    private float NormalRandom(float min, float max)
    {
        // Simple approximation of Park-Miller normal distribution
        float sum = 0;
        for (int i = 0; i < 3; i++) // More iterations = more normal
        {
            sum += _rng.RandfRange(min, max);
        }
        
        return sum / 3.0f;
    }
    
    private void SeparateCells(int iterations)
    {
        for (int iter = 0; iter < iterations; iter++)
        {
            SeparateCellsStep();
        }
    }
    
    private void SeparateCellsStep()
    {
        
        for (int i = 0; i < _cells.Count; i++)
        {
            Vector2 moveVector = Vector2.Zero;
            Rect2 cellA = _cells[i];
            
            for (int j = 0; j < _cells.Count; j++)
            {
                if (i == j) continue;
                
                Rect2 cellB = _cells[j];
                
                if (cellA.Intersects(cellB))
                {
                    // Calculate overlap and push direction
                    float overlapX = Math.Min(
                        cellA.Position.X + cellA.Size.X - cellB.Position.X,
                        cellB.Position.X + cellB.Size.X - cellA.Position.X
                    );
                    
                    float overlapY = Math.Min(
                        cellA.Position.Y + cellA.Size.Y - cellB.Position.Y,
                        cellB.Position.Y + cellB.Size.Y - cellA.Position.Y
                    );
                    
                    // Determine which axis has the smallest overlap
                    if (overlapX < overlapY)
                    {
                        float dir = cellA.GetCenter().X < cellB.GetCenter().X ? -1 : 1;
                        moveVector.X += dir * (overlapX / 2 + 1);
                    }
                    else
                    {
                        float dir = cellA.GetCenter().Y < cellB.GetCenter().Y ? -1 : 1;
                        moveVector.Y += dir * (overlapY / 2 + 1);
                    }
                    
                }
            }
            
            // Apply movement to cell
            if (moveVector != Vector2.Zero)
            {
                _cells[i] = new Rect2(
                    cellA.Position + moveVector,
                    cellA.Size
                );
            }
        }
    }
    
    private void DetermineRooms()
    {
        _rooms.Clear();
        _roomCenters.Clear();
        
        foreach (Rect2 cell in _cells)
        {
            // Konwertuj minimalne wymiary na piksele
            float minWidthInPixels = MinRoomWidth * TileSize;
            float minHeightInPixels = MinRoomHeight * TileSize;
            
            if (cell.Size.X >= minWidthInPixels && cell.Size.Y >= minHeightInPixels)
            {
                _rooms.Add(cell);
                _roomCenters.Add(cell.GetCenter());
            }
        }
    }
    
    private void ConnectRooms()
    {
        if (_rooms.Count < 2) return;
        
        // Step 6: Delaunay Triangulation (simplified approach using distance)
        List<(int, int, float)> edges = new List<(int, int, float)>();
        
        for (int i = 0; i < _roomCenters.Count; i++)
        {
            for (int j = i + 1; j < _roomCenters.Count; j++)
            {
                float distance = _roomCenters[i].DistanceTo(_roomCenters[j]);
                edges.Add((i, j, distance));
            }
        }
        
        // Sort edges by distance
        edges.Sort((a, b) => a.Item3.CompareTo(b.Item3));
        
        // Step 7: Minimal Spanning Tree
        List<(int, int)> mstEdges = new List<(int, int)>();
        int[] parent = new int[_roomCenters.Count];
        
        for (int i = 0; i < parent.Length; i++)
        {
            parent[i] = i;
        }
        
        foreach (var edge in edges)
        {
            int root1 = FindRoot(parent, edge.Item1);
            int root2 = FindRoot(parent, edge.Item2);
            
            if (root1 != root2)
            {
                mstEdges.Add((edge.Item1, edge.Item2));
                parent[root1] = root2;
            }
        }
        
        // Step 8: Re-add some of the remaining edges to create loops
        _corridorEdges.Clear();
        _corridorEdges.AddRange(mstEdges);
        
        List<(int, int)> remainingEdges = new List<(int, int)>();
        
        foreach (var edge in edges)
        {
            if (!mstEdges.Contains((edge.Item1, edge.Item2)) && 
                !mstEdges.Contains((edge.Item2, edge.Item1)))
            {
                remainingEdges.Add((edge.Item1, edge.Item2));
            }
        }
        
        // Shuffle remaining edges
        remainingEdges = remainingEdges.OrderBy(x => _rng.RandiRange(0, 1000)).ToList();
        
        // Add a percentage of the remaining edges
        int edgesToAdd = (int)(remainingEdges.Count * LoopPercent);
        
        for (int i = 0; i < edgesToAdd && i < remainingEdges.Count; i++)
        {
            _corridorEdges.Add(remainingEdges[i]);
        }
    }
    
    private int FindRoot(int[] parent, int i)
    {
        if (parent[i] != i)
        {
            parent[i] = FindRoot(parent, parent[i]);
        }
        return parent[i];
    }
    
    private void CreateCorridors()
    {
        foreach (var edge in _corridorEdges)
        {
            Vector2 start = _roomCenters[edge.Item1];
            Vector2 end = _roomCenters[edge.Item2];
            
            // Determine whether to create an L-shaped corridor or straight line
            bool useLShape = _rng.Randf() > 0.5f;
            
            if (useLShape)
            {
                // Create L-shaped corridor
                Vector2 corner;
                
                if (_rng.Randf() > 0.5f)
                {
                    // Horizontal then vertical
                    corner = new Vector2(end.X, start.Y);
                }
                else
                {
                    // Vertical then horizontal
                    corner = new Vector2(start.X, end.Y);
                }
                
                // Apply corridor for both segments
                ApplyCorridorBetweenPoints(start, corner);
                ApplyCorridorBetweenPoints(corner, end);
            }
            else
            {
                // Create straight corridor
                ApplyCorridorBetweenPoints(start, end);
            }
        }
    }
    
    private void ApplyCorridorBetweenPoints(Vector2 start, Vector2 end)
    {
        Vector2 direction = (end - start).Normalized();
        float distance = start.DistanceTo(end);
        
        // Find all cells that intersect with this corridor line
        foreach (Rect2 cell in _cells)
        {
            if (_rooms.Contains(cell)) continue; // Skip rooms
            
            // Check if the cell intersects with the corridor line
            if (LineIntersectsRect(start, end, cell))
            {
                // Mark this cell as a corridor
                _rooms.Add(cell); // For rendering purposes
            }
        }
    }
    
    private bool LineIntersectsRect(Vector2 lineStart, Vector2 lineEnd, Rect2 rect)
    {
        // Simple check: does the line intersect any of the 4 sides of the rectangle?
        Vector2[] rectCorners = {
            rect.Position,
            new Vector2(rect.End.X, rect.Position.Y),
            rect.End,
            new Vector2(rect.Position.X, rect.End.Y)
        };
        
        for (int i = 0; i < 4; i++)
        {
            int next = (i + 1) % 4;
            if (LineSegmentsIntersect(lineStart, lineEnd, rectCorners[i], rectCorners[next]))
            {
                return true;
            }
        }
        
        // Also check if either endpoint is inside the rectangle
        if (rect.HasPoint(lineStart) || rect.HasPoint(lineEnd))
        {
            return true;
        }
        
        return false;
    }
    
    private bool LineSegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
    {
        // Check if line segments AB and CD intersect
        float denominator = ((b.X - a.X) * (d.Y - c.Y)) - ((b.Y - a.Y) * (d.X - c.X));
        
        if (denominator == 0) return false; // Lines are parallel
        
        float numerator1 = ((a.Y - c.Y) * (d.X - c.X)) - ((a.X - c.X) * (d.Y - c.Y));
        float numerator2 = ((a.Y - c.Y) * (b.X - a.X)) - ((a.X - c.X) * (b.Y - a.Y));
        
        if (numerator1 == 0 || numerator2 == 0) return false; // Lines are coincident
        
        float r = numerator1 / denominator;
        float s = numerator2 / denominator;
        
        return (r > 0 && r < 1) && (s > 0 && s < 1);
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
                    // Using TileMapLayer.SetCell with proper parameters
                    FloorLayer.SetCell(new Vector2I(x, y), 0, new Vector2I(0, 0), 0);
                }
            }
        }
        
        // Then place wall tiles around floor tiles
        int searchRadius = (int)(CellSpawnRadius * 2); // Powiększony obszar poszukiwania
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
    
    // Metody wizualizacji
    private void VisualizeAllCells()
    {
        // Wyczyść poprzednią wizualizację komórek
        foreach (Node child in _cellsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Dodaj nowe prostokąty reprezentujące komórki
        foreach (Rect2 cell in _cells)
        {
            ColorRect rect = new ColorRect();
            rect.Position = cell.Position;
            rect.Size = cell.Size;
            rect.Color = new Color(0.5f, 0.5f, 0.8f, 0.5f);
            _cellsVisContainer.AddChild(rect);
            
            // Dodajemy siatkę do wizualizacji kafelków wewnątrz komórki
            for (float x = 0; x < cell.Size.X; x += TileSize)
            {
                Line2D line = new Line2D();
                line.Width = 1.0f;
                line.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                line.AddPoint(new Vector2(cell.Position.X + x, cell.Position.Y));
                line.AddPoint(new Vector2(cell.Position.X + x, cell.Position.Y + cell.Size.Y));
                _cellsVisContainer.AddChild(line);
            }
            
            for (float y = 0; y < cell.Size.Y; y += TileSize)
            {
                Line2D line = new Line2D();
                line.Width = 1.0f;
                line.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                line.AddPoint(new Vector2(cell.Position.X, cell.Position.Y + y));
                line.AddPoint(new Vector2(cell.Position.X + cell.Size.X, cell.Position.Y + y));
                _cellsVisContainer.AddChild(line);
            }
        }
    }
    
    private void VisualizeRooms()
    {
        // Wyczyść poprzednią wizualizację pokoi
        foreach (Node child in _roomsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Dodaj nowe prostokąty reprezentujące pokoje
        foreach (Rect2 room in _rooms)
        {
            ColorRect rect = new ColorRect();
            rect.Position = room.Position;
            rect.Size = room.Size;
            rect.Color = new Color(0.2f, 0.8f, 0.2f, 0.5f);
            _roomsVisContainer.AddChild(rect);
            
            // Dodaj punkt centralny
            ColorRect center = new ColorRect();
            center.Position = room.GetCenter() - new Vector2(5, 5);
            center.Size = new Vector2(10, 10);
            center.Color = new Color(1, 0, 0, 1);
            _roomsVisContainer.AddChild(center);
            
            // Dodajemy siatkę kafelków
            for (float x = 0; x < room.Size.X; x += TileSize)
            {
                Line2D line = new Line2D();
                line.Width = 1.0f;
                line.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                line.AddPoint(new Vector2(room.Position.X + x, room.Position.Y));
                line.AddPoint(new Vector2(room.Position.X + x, room.Position.Y + room.Size.Y));
                _roomsVisContainer.AddChild(line);
            }
            
            for (float y = 0; y < room.Size.Y; y += TileSize)
            {
                Line2D line = new Line2D();
                line.Width = 1.0f;
                line.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                line.AddPoint(new Vector2(room.Position.X, room.Position.Y + y));
                line.AddPoint(new Vector2(room.Position.X + room.Size.X, room.Position.Y + y));
                _roomsVisContainer.AddChild(line);
            }
        }
    }
    
    private void VisualizeDelaunayTriangulation()
    {
        // Wyczyść poprzednią wizualizację triangulacji
        foreach (Node child in _delaunayVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Wyświetl wszystkie możliwe połączenia (uproszczona triangulacja Delaunay)
        for (int i = 0; i < _roomCenters.Count; i++)
        {
            for (int j = i + 1; j < _roomCenters.Count; j++)
            {
                Line2D line = new Line2D();
                line.Width = 1.0f;
                line.DefaultColor = new Color(0.8f, 0.8f, 0.2f, 0.3f);
                line.AddPoint(_roomCenters[i]);
                line.AddPoint(_roomCenters[j]);
                _delaunayVisContainer.AddChild(line);
            }
        }
    }
    
    private void VisualizeMinimalSpanningTree()
    {
        // Wyczyść poprzednią wizualizację MST
        foreach (Node child in _mstVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Wyświetl krawędzie MST
        foreach (var edge in _corridorEdges)
        {
            Line2D line = new Line2D();
            line.Width = 3.0f;
            line.DefaultColor = new Color(0.2f, 0.8f, 0.8f, 0.7f);
            line.AddPoint(_roomCenters[edge.Item1]);
            line.AddPoint(_roomCenters[edge.Item2]);
            _mstVisContainer.AddChild(line);
        }
    }
    
    private void VisualizeLoops()
    {
        // Usuń wszystkie krawędzie z wizualizacji MST
        foreach (Node child in _mstVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Wyczyść poprzednią wizualizację pętli
        foreach (Node child in _loopsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Wyświetl wszystkie krawędzie (MST + pętle)
        foreach (var edge in _corridorEdges)
        {
            Line2D line = new Line2D();
            line.Width = 3.0f;
            line.DefaultColor = new Color(0.9f, 0.4f, 0.1f, 0.7f);
            line.AddPoint(_roomCenters[edge.Item1]);
            line.AddPoint(_roomCenters[edge.Item2]);
            _loopsVisContainer.AddChild(line);
        }
    }
    
    private void VisualizeCorridors()
    {
        // Wyczyść poprzednią wizualizację korytarzy
        foreach (Node child in _corridorsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Pokaż komórki, które stały się korytarzami w innym kolorze
        foreach (Rect2 cell in _cells)
        {
            if (_rooms.Contains(cell))
            {
                bool isOriginalRoom = false;
                foreach (Vector2 center in _roomCenters)
                {
                    if (cell.HasPoint(center))
                    {
                        isOriginalRoom = true;
                        break;
                    }
                }
                
                if (!isOriginalRoom)
                {
                    // To jest korytarz
                    ColorRect rect = new ColorRect();
                    rect.Position = cell.Position;
                    rect.Size = cell.Size;
                    rect.Color = new Color(0.9f, 0.5f, 0.1f, 0.6f);
                    _corridorsVisContainer.AddChild(rect);
                }
            }
        }
    }
}