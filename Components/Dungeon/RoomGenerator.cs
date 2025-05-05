using Godot;
using System;
using System.Collections.Generic;

public partial class RoomGenerator : Node2D
{
    [Export] public int NumberOfCells = 150;
    [Export] public float CellSpawnRadius = 20.0f;
    [Export] public float CellSpawnRadiusX = 20.0f;
    [Export] public float CellSpawnRadiusY = 20.0f;
    [Export] public int TileSize = 16;
    
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    
    public override void _Ready()
    {
        // Don't randomize in _Ready, we'll use SetSeed
    }
    
    public void SetSeed(int seed)
    {
        _rng.Seed = (ulong)seed;
    }
    
    public List<Rect2I> GenerateCells()
    {
        List<Rect2I> cells = new List<Rect2I>();
        
        // Initialize ellipse radii - if they're still default values, use CellSpawnRadius for both
        float radiusX = CellSpawnRadiusX == 20.0f ? CellSpawnRadius : CellSpawnRadiusX;
        float radiusY = CellSpawnRadiusY == 20.0f ? CellSpawnRadius : CellSpawnRadiusY;
        
        for (int i = 0; i < NumberOfCells; i++)
        {
            // Generate sizes in tile units (integer values)
            int widthInTiles = _rng.RandiRange(15, 31);
            int heightInTiles = _rng.RandiRange(7, 16);
            
            // Convert sizes to pixels (aligned to grid)
            int width = widthInTiles * TileSize;
            int height = heightInTiles * TileSize;
            
            // Generate random position within an ellipse
            float angle = _rng.RandfRange(0, Mathf.Pi * 2);
            float t = _rng.RandfRange(0, 1.0f); // Random value between 0 and 1
            float distance = Mathf.Sqrt(t); // Square root for uniform distribution in ellipse
            
            // Calculate position in tile coordinates using elliptical equations
            int tileX = Mathf.FloorToInt(Mathf.Cos(angle) * radiusX * distance);
            int tileY = Mathf.FloorToInt(Mathf.Sin(angle) * radiusY * distance);
            
            // Convert to pixel coordinates
            int pixelX = tileX * TileSize;
            int pixelY = tileY * TileSize;
            
            cells.Add(new Rect2I(pixelX, pixelY, width, height));
        }
        
        return cells;
    }
}