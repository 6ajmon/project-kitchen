using Godot;
using System;
using System.Collections.Generic;

public partial class RoomGenerator : Node2D
{
    [Export] public int NumberOfCells = 150;
    [Export] public float CellSpawnRadius = 20.0f;
    [Export] public int TileSize = 16;
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    
    public override void _Ready()
    {
        _rng.Randomize();
    }
    
    public List<Rect2I> GenerateCells()
    {
        List<Rect2I> cells = new List<Rect2I>();
        
        for (int i = 0; i < NumberOfCells; i++)
        {
            // Generate sizes in tile units (integer values)
            int widthInTiles = _rng.RandiRange(5, 13);
            int heightInTiles = _rng.RandiRange(5, 13);
            
            // Convert sizes to pixels (aligned to grid)
            int width = widthInTiles * TileSize;
            int height = heightInTiles * TileSize;
            
            // Generate random position within radius
            float angle = _rng.RandfRange(0, Mathf.Pi * 2);
            float distance = _rng.RandfRange(0, CellSpawnRadius);
            
            // Calculate position in tile coordinates
            int tileX = Mathf.FloorToInt(Mathf.Cos(angle) * distance);
            int tileY = Mathf.FloorToInt(Mathf.Sin(angle) * distance);
            
            // Convert to pixel coordinates
            int pixelX = tileX * TileSize;
            int pixelY = tileY * TileSize;
            
            cells.Add(new Rect2I(pixelX, pixelY, width, height));
        }
        
        return cells;
    }
}