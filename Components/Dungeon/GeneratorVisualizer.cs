using Godot;
using System;
using System.Collections.Generic;

public partial class GeneratorVisualizer : Node2D
{
    [Export] public int TileSize = 16;
    
    // Visualization containers
    private Node2D _cellsVisContainer;
    private Node2D _roomsVisContainer;
    private Node2D _delaunayVisContainer;
    private Node2D _mstVisContainer;
    private Node2D _loopsVisContainer;
    private Node2D _corridorsVisContainer;
    
    public override void _Ready()
    {
        InitializeContainers();
    }
    
    private void InitializeContainers()
    {
        _cellsVisContainer = new Node2D();
        _cellsVisContainer.Name = "CellsContainer";
        AddChild(_cellsVisContainer);
        
        _roomsVisContainer = new Node2D();
        _roomsVisContainer.Name = "RoomsContainer";
        AddChild(_roomsVisContainer);
        
        _delaunayVisContainer = new Node2D();
        _delaunayVisContainer.Name = "DelaunayContainer";
        AddChild(_delaunayVisContainer);
        
        _mstVisContainer = new Node2D();
        _mstVisContainer.Name = "MSTContainer";
        AddChild(_mstVisContainer);
        
        _loopsVisContainer = new Node2D();
        _loopsVisContainer.Name = "LoopsContainer";
        AddChild(_loopsVisContainer);
        
        _corridorsVisContainer = new Node2D();
        _corridorsVisContainer.Name = "CorridorsContainer";
        AddChild(_corridorsVisContainer);
    }
    
    public void VisualizeAllCells(List<Rect2> cells)
    {
        // Clear previous cell visualization
        foreach (Node child in _cellsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add new rectangles representing cells
        foreach (Rect2 cell in cells)
        {
            ColorRect rect = new ColorRect();
            rect.Position = cell.Position;
            rect.Size = cell.Size;
            rect.Color = new Color(0.5f, 0.5f, 0.8f, 0.5f);
            _cellsVisContainer.AddChild(rect);
            
            // Add grid for tile visualization inside the cell
            for (float x = 0; x < cell.Size.X; x += TileSize)
            {
                for (float y = 0; y < cell.Size.Y; y += TileSize)
                {
                    Line2D gridLine = new Line2D();
                    gridLine.Width = 1.0f;
                    gridLine.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                    
                    // Horizontal lines
                    gridLine.AddPoint(new Vector2(cell.Position.X, cell.Position.Y + y));
                    gridLine.AddPoint(new Vector2(cell.Position.X + cell.Size.X, cell.Position.Y + y));
                    _cellsVisContainer.AddChild(gridLine);
                    
                    // Vertical lines
                    Line2D vLine = new Line2D();
                    vLine.Width = 1.0f;
                    vLine.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                    vLine.AddPoint(new Vector2(cell.Position.X + x, cell.Position.Y));
                    vLine.AddPoint(new Vector2(cell.Position.X + x, cell.Position.Y + cell.Size.Y));
                    _cellsVisContainer.AddChild(vLine);
                }
            }
        }
    }
    
    public void VisualizeRooms(List<Rect2> rooms, List<Vector2> roomCenters)
    {
        // Clear previous room visualization
        foreach (Node child in _roomsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add room visualization (different color from regular cells)
        for (int i = 0; i < rooms.Count; i++)
        {
            Rect2 room = rooms[i];
            Vector2 center = roomCenters[i];
            
            // Room rectangle
            ColorRect rect = new ColorRect();
            rect.Position = room.Position;
            rect.Size = room.Size;
            rect.Color = new Color(0.8f, 0.4f, 0.4f, 0.5f);
            _roomsVisContainer.AddChild(rect);
            
            // Room center marker
            ColorRect centerMarker = new ColorRect();
            centerMarker.Position = center - new Vector2(5, 5);
            centerMarker.Size = new Vector2(10, 10);
            centerMarker.Color = new Color(1.0f, 0.2f, 0.2f, 0.8f);
            _roomsVisContainer.AddChild(centerMarker);
            
            // Room number label
            Label label = new Label();
            label.Text = i.ToString();
            label.Position = center - new Vector2(10, 10);
            label.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.8f);
            _roomsVisContainer.AddChild(label);
        }
    }
    
    public void VisualizeDelaunayTriangulation(List<Vector2> roomCenters, List<(int, int)> edges)
    {
        // Clear previous triangulation visualization
        foreach (Node child in _delaunayVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add lines for each Delaunay edge
        foreach (var edge in edges)
        {
            Line2D line = new Line2D();
            line.Width = 2.0f;
            line.DefaultColor = new Color(0.2f, 0.6f, 0.8f, 0.5f);
            line.AddPoint(roomCenters[edge.Item1]);
            line.AddPoint(roomCenters[edge.Item2]);
            _delaunayVisContainer.AddChild(line);
        }
    }
    
    public void VisualizeMinimalSpanningTree(List<Vector2> roomCenters, List<(int, int)> mstEdges)
    {
        // Clear previous MST visualization
        foreach (Node child in _mstVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add lines for each MST edge (thicker than Delaunay)
        foreach (var edge in mstEdges)
        {
            Line2D line = new Line2D();
            line.Width = 4.0f;
            line.DefaultColor = new Color(0.2f, 0.8f, 0.4f, 0.7f);
            line.AddPoint(roomCenters[edge.Item1]);
            line.AddPoint(roomCenters[edge.Item2]);
            _mstVisContainer.AddChild(line);
        }
    }
    
    public void VisualizeLoops(List<Vector2> roomCenters, List<(int, int)> corridorEdges)
    {
        // Clear previous loops visualization
        foreach (Node child in _loopsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add lines for each corridor edge (if not in MST, it's a loop)
        foreach (var edge in corridorEdges)
        {
            // Skip if the edge is already in the MST container
            bool isInMst = false;
            foreach (Line2D mstLine in _mstVisContainer.GetChildren())
            {
                if ((mstLine.Points[0] == roomCenters[edge.Item1] && mstLine.Points[1] == roomCenters[edge.Item2]) ||
                    (mstLine.Points[0] == roomCenters[edge.Item2] && mstLine.Points[1] == roomCenters[edge.Item1]))
                {
                    isInMst = true;
                    break;
                }
            }
            
            if (!isInMst)
            {
                Line2D line = new Line2D();
                line.Width = 3.0f;
                line.DefaultColor = new Color(0.8f, 0.6f, 0.2f, 0.7f);
                line.AddPoint(roomCenters[edge.Item1]);
                line.AddPoint(roomCenters[edge.Item2]);
                _loopsVisContainer.AddChild(line);
            }
        }
    }
    
    public void VisualizeCorridors(List<Rect2> cells, List<Rect2> rooms, List<Vector2> roomCenters)
    {
        // Clear previous corridor visualization
        foreach (Node child in _corridorsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Highlight cells that are corridors (cells that are in rooms but weren't in the original rooms list)
        foreach (Rect2 cell in cells)
        {
            if (IsCorridorCell(cell, rooms))
            {
                ColorRect rect = new ColorRect();
                rect.Position = cell.Position;
                rect.Size = cell.Size;
                rect.Color = new Color(0.3f, 0.8f, 0.3f, 0.6f);
                _corridorsVisContainer.AddChild(rect);
                
                // Add grid lines for the corridor cells
                for (float x = 0; x < cell.Size.X; x += TileSize)
                {
                    for (float y = 0; y < cell.Size.Y; y += TileSize)
                    {
                        // Horizontal grid lines
                        Line2D hLine = new Line2D();
                        hLine.Width = 1.0f;
                        hLine.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                        hLine.AddPoint(new Vector2(cell.Position.X, cell.Position.Y + y));
                        hLine.AddPoint(new Vector2(cell.Position.X + cell.Size.X, cell.Position.Y + y));
                        _corridorsVisContainer.AddChild(hLine);
                        
                        // Vertical grid lines
                        Line2D vLine = new Line2D();
                        vLine.Width = 1.0f;
                        vLine.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
                        vLine.AddPoint(new Vector2(cell.Position.X + x, cell.Position.Y));
                        vLine.AddPoint(new Vector2(cell.Position.X + x, cell.Position.Y + cell.Size.Y));
                        _corridorsVisContainer.AddChild(vLine);
                    }
                }
            }
        }
    }
    
    private bool IsCorridorCell(Rect2 cell, List<Rect2> rooms)
    {
        // Check if this cell is in the rooms list but wasn't an original room
        // A corridor cell is one that is in the final rooms list but wasn't in the original rooms list
        bool isInFinalRooms = false;
        
        foreach (Rect2 room in rooms)
        {
            if (AreRectsEqual(cell, room))
            {
                isInFinalRooms = true;
                break;
            }
        }
        
        // Also check if the cell meets the criteria to be a room (same as in MainRoomDeterminator)
        // If it's not a room, but it's in the final list, then it's a corridor
        float minWidthInPixels = 6 * TileSize;  // Using default values, could be parameterized
        float minHeightInPixels = 6 * TileSize;
        
        bool isRoom = cell.Size.X >= minWidthInPixels && cell.Size.Y >= minHeightInPixels;
        
        return isInFinalRooms && !isRoom;
    }
    
    private bool AreRectsEqual(Rect2 rect1, Rect2 rect2)
    {
        // Check if two Rect2 objects represent the same rectangle
        // Using small epsilon for floating point comparison
        const float epsilon = 0.001f;
        
        return Math.Abs(rect1.Position.X - rect2.Position.X) < epsilon &&
               Math.Abs(rect1.Position.Y - rect2.Position.Y) < epsilon &&
               Math.Abs(rect1.Size.X - rect2.Size.X) < epsilon &&
               Math.Abs(rect1.Size.Y - rect2.Size.Y) < epsilon;
    }
}