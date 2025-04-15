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
    
    public List<Rect2> GenerateCells()
    {
        List<Rect2> cells = new List<Rect2>();
        
        for (int i = 0; i < NumberOfCells; i++)
        {
            // Generate sizes in tile units
            float widthInTiles = NormalRandom(2, 12);  // From 2 to 12 tiles wide
            float heightInTiles = NormalRandom(2, 12); // From 2 to 12 tiles high
            
            // Ensure width/height ratio is reasonable
            // if (widthInTiles / heightInTiles > 2.5f) heightInTiles = widthInTiles / 2.0f;
            // if (heightInTiles / widthInTiles > 2.5f) widthInTiles = heightInTiles / 2.0f;
            
            // Convert sizes to pixels
            float width = widthInTiles * TileSize;
            float height = heightInTiles * TileSize;
            
            // Generate random position within radius (also in tile units)
            float angle = _rng.RandfRange(0, Mathf.Pi * 2);
            float distance = _rng.RandfRange(0, CellSpawnRadius);
            Vector2 position = new Vector2(
                Mathf.Cos(angle) * distance * TileSize,
                Mathf.Sin(angle) * distance * TileSize
            );
            
            cells.Add(new Rect2(position.X, position.Y, width, height));
        }
        
        return cells;
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
}