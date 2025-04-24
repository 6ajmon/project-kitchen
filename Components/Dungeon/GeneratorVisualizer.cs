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
    
    // Store previous positions to show movement
    private List<Rect2I> _previousCellPositions = new List<Rect2I>();
    
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
    
    public void VisualizeAllCells(List<Rect2I> cells)
    {
        // Clear previous cell visualization
        foreach (Node child in _cellsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add new rectangles representing cells
        for (int i = 0; i < cells.Count; i++)
        {
            Rect2I cell = cells[i];
            
            // Create the cell rectangle
            ColorRect rect = new ColorRect();
            rect.Position = new Vector2(cell.Position.X, cell.Position.Y);
            rect.Size = new Vector2(cell.Size.X, cell.Size.Y);
            rect.Color = new Color(0.5f, 0.5f, 0.8f, 0.5f);
            _cellsVisContainer.AddChild(rect);
            
            // Add velocity indicator if we have previous positions
            if (_previousCellPositions.Count == cells.Count)
            {
                Rect2I prevCell = _previousCellPositions[i];
                Vector2 prevCenter = new Vector2(
                    prevCell.Position.X + prevCell.Size.X / 2,
                    prevCell.Position.Y + prevCell.Size.Y / 2
                );
                
                Vector2 currentCenter = new Vector2(
                    cell.Position.X + cell.Size.X / 2,
                    cell.Position.Y + cell.Size.Y / 2
                );
                
                // Only draw movement indicator if there's significant movement
                if (prevCenter.DistanceSquaredTo(currentCenter) > 4)
                {
                    Line2D velocityLine = new Line2D();
                    velocityLine.Width = 2.0f;
                    velocityLine.DefaultColor = new Color(1, 0, 0, 0.7f); // Red for movement
                    velocityLine.AddPoint(currentCenter);
                    velocityLine.AddPoint(prevCenter);
                    _cellsVisContainer.AddChild(velocityLine);
                }
            }
            
            // Add outline
            Line2D outlineRect = new Line2D();
            outlineRect.Width = 1.0f;
            outlineRect.DefaultColor = new Color(0.7f, 0.7f, 0.7f, 0.3f);
            outlineRect.AddPoint(new Vector2(cell.Position.X, cell.Position.Y));
            outlineRect.AddPoint(new Vector2(cell.Position.X + cell.Size.X, cell.Position.Y));
            outlineRect.AddPoint(new Vector2(cell.Position.X + cell.Size.X, cell.Position.Y + cell.Size.Y));
            outlineRect.AddPoint(new Vector2(cell.Position.X, cell.Position.Y + cell.Size.Y));
            outlineRect.AddPoint(new Vector2(cell.Position.X, cell.Position.Y));
            _cellsVisContainer.AddChild(outlineRect);
        }
        
        // Store current positions as previous for next update
        _previousCellPositions = new List<Rect2I>(cells);
    }
    
    public void VisualizeRooms(List<Rect2I> rooms, List<Vector2I> roomCenters)
    {
        // Clear previous room visualization
        foreach (Node child in _roomsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Add room visualization (different color from regular cells)
        for (int i = 0; i < rooms.Count; i++)
        {
            Rect2I room = rooms[i];
            Vector2I center = roomCenters[i];
            
            // Room rectangle
            ColorRect rect = new ColorRect();
            rect.Position = new Vector2(room.Position.X, room.Position.Y);
            rect.Size = new Vector2(room.Size.X, room.Size.Y);
            rect.Color = new Color(0.8f, 0.4f, 0.4f, 0.5f);
            _roomsVisContainer.AddChild(rect);
            
            // Room center marker
            ColorRect centerMarker = new ColorRect();
            centerMarker.Position = new Vector2(center.X - 5, center.Y - 5);
            centerMarker.Size = new Vector2(10, 10);
            centerMarker.Color = new Color(1.0f, 0.2f, 0.2f, 0.8f);
            _roomsVisContainer.AddChild(centerMarker);
            
            // Room number label
            Label label = new Label();
            label.Text = i.ToString();
            label.Position = new Vector2(center.X - 10, center.Y - 10);
            label.Modulate = new Color(1.0f, 1.0f, 1.0f, 0.8f);
            _roomsVisContainer.AddChild(label);
        }
    }
    
    public void VisualizeDelaunayTriangulation(List<Vector2I> roomCenters, List<(int, int)> edges)
    {
        // Clear previous triangulation visualization
        foreach (Node child in _delaunayVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        if (edges.Count == 0 || roomCenters.Count == 0)
        {
            GD.PrintErr("No edges or room centers to visualize in Delaunay triangulation");
            return;
        }
        
        // Add lines for each Delaunay edge
        int validEdges = 0;
        foreach (var edge in edges)
        {
            if (edge.Item1 < 0 || edge.Item1 >= roomCenters.Count || 
                edge.Item2 < 0 || edge.Item2 >= roomCenters.Count)
            {
                GD.PrintErr($"Invalid edge index ({edge.Item1}, {edge.Item2}) for room centers count {roomCenters.Count}");
                continue;
            }
            
            Line2D line = new Line2D();
            line.Width = 2.0f;
            line.DefaultColor = new Color(0.2f, 0.6f, 0.8f, 0.5f);
            line.AddPoint(new Vector2(roomCenters[edge.Item1].X, roomCenters[edge.Item1].Y));
            line.AddPoint(new Vector2(roomCenters[edge.Item2].X, roomCenters[edge.Item2].Y));
            _delaunayVisContainer.AddChild(line);
            validEdges++;
        }
        
        GD.Print($"Visualized {validEdges} Delaunay edges");
    }
    
    public void VisualizeMinimalSpanningTree(List<Vector2I> roomCenters, List<(int, int)> mstEdges)
    {
        // Clear previous MST visualization
        foreach (Node child in _mstVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        if (mstEdges.Count == 0 || roomCenters.Count == 0)
        {
            GD.PrintErr("No edges or room centers to visualize in MST");
            return;
        }
        
        // Add lines for each MST edge (thicker than Delaunay)
        int validEdges = 0;
        foreach (var edge in mstEdges)
        {
            if (edge.Item1 < 0 || edge.Item1 >= roomCenters.Count || 
                edge.Item2 < 0 || edge.Item2 >= roomCenters.Count)
            {
                GD.PrintErr($"Invalid edge index ({edge.Item1}, {edge.Item2}) for room centers count {roomCenters.Count}");
                continue;
            }
            
            Line2D line = new Line2D();
            line.Width = 4.0f;
            line.DefaultColor = new Color(0.2f, 0.8f, 0.4f, 0.7f);
            line.AddPoint(new Vector2(roomCenters[edge.Item1].X, roomCenters[edge.Item1].Y));
            line.AddPoint(new Vector2(roomCenters[edge.Item2].X, roomCenters[edge.Item2].Y));
            _mstVisContainer.AddChild(line);
            validEdges++;
        }
        
        GD.Print($"Visualized {validEdges} MST edges");
    }
    
    public void VisualizeLoops(List<Vector2I> roomCenters, List<(int, int)> corridorEdges)
    {
        // Clear previous loops visualization
        foreach (Node child in _loopsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        if (corridorEdges.Count == 0 || roomCenters.Count == 0)
        {
            GD.PrintErr("No edges or room centers to visualize in loops");
            return;
        }
        
        // Add lines for each corridor edge (if not in MST, it's a loop)
        int loopEdges = 0;
        foreach (var edge in corridorEdges)
        {
            if (edge.Item1 < 0 || edge.Item1 >= roomCenters.Count || 
                edge.Item2 < 0 || edge.Item2 >= roomCenters.Count)
            {
                GD.PrintErr($"Invalid edge index ({edge.Item1}, {edge.Item2}) for room centers count {roomCenters.Count}");
                continue;
            }
            
            // Skip if the edge is already in the MST container
            bool isInMst = false;
            foreach (Line2D mstLine in _mstVisContainer.GetChildren())
            {
                if ((mstLine.Points.Length >= 2) && 
                    ((mstLine.Points[0] == new Vector2(roomCenters[edge.Item1].X, roomCenters[edge.Item1].Y) &&
                     mstLine.Points[1] == new Vector2(roomCenters[edge.Item2].X, roomCenters[edge.Item2].Y)) ||
                    (mstLine.Points[0] == new Vector2(roomCenters[edge.Item2].X, roomCenters[edge.Item2].Y) &&
                     mstLine.Points[1] == new Vector2(roomCenters[edge.Item1].X, roomCenters[edge.Item1].Y))))
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
                line.AddPoint(new Vector2(roomCenters[edge.Item1].X, roomCenters[edge.Item1].Y));
                line.AddPoint(new Vector2(roomCenters[edge.Item2].X, roomCenters[edge.Item2].Y));
                _loopsVisContainer.AddChild(line);
                loopEdges++;
            }
        }
        
        GD.Print($"Visualized {loopEdges} loop edges");
    }
    
    public void VisualizeCorridors(List<Rect2I> cells, List<Rect2I> rooms, List<Vector2I> roomCenters)
    {
        // Clear previous corridor visualization
        foreach (Node child in _corridorsVisContainer.GetChildren())
        {
            child.QueueFree();
        }
        
        // Highlight cells that are corridors (cells that are in rooms but weren't in the original rooms list)
        foreach (Rect2I cell in cells)
        {
            if (IsCorridorCell(cell, rooms))
            {
                ColorRect rect = new ColorRect();
                rect.Position = new Vector2(cell.Position.X, cell.Position.Y);
                rect.Size = new Vector2(cell.Size.X, cell.Size.Y);
                rect.Color = new Color(0.3f, 0.8f, 0.3f, 0.6f);
                _corridorsVisContainer.AddChild(rect);
                
                // Add grid lines for the corridor cells
                for (int x = 0; x < cell.Size.X; x += TileSize)
                {
                    for (int y = 0; y < cell.Size.Y; y += TileSize)
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
    
    private bool IsCorridorCell(Rect2I cell, List<Rect2I> rooms)
    {
        // Check if this cell is in the rooms list but wasn't an original room
        // A corridor cell is one that is in the final rooms list but wasn't in the original rooms list
        bool isInFinalRooms = false;
        
        foreach (Rect2I room in rooms)
        {
            if (AreRectsEqual(cell, room))
            {
                isInFinalRooms = true;
                break;
            }
        }
        
        // Also check if the cell meets the criteria to be a room (same as in MainRoomDeterminator)
        // If it's not a room, but it's in the final list, then it's a corridor
        int minWidthInPixels = 6 * TileSize;  // Using default values, could be parameterized
        int minHeightInPixels = 6 * TileSize;
        
        bool isRoom = cell.Size.X >= minWidthInPixels && cell.Size.Y >= minHeightInPixels;
        
        return isInFinalRooms && !isRoom;
    }
    
    private bool AreRectsEqual(Rect2I rect1, Rect2I rect2)
    {
        // Direct comparison for integer-based rectangles
        return rect1.Position == rect2.Position && rect1.Size == rect2.Size;
    }
}