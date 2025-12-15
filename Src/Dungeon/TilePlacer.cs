using Godot;
using System;
using System.Collections.Generic;

public enum DungeonTileType
{
    None,
    Floor,
    Wall
}

public partial class TilePlacer : Node2D
{
    public Vector2I FloorAtlasCoord = new Vector2I(1, 0); // Default floor tile coord
    public Vector2I WallAtlasCoord = new Vector2I(0, 0);  // Default wall tile coord
    
    // These are the relative positions of the 4 display tiles around a world tile
    private readonly Vector2I[] DISPLAY_NEIGHBORS = new Vector2I[] { new(0, 0), new(1, 0), new(0, 1), new(1, 1) };
    
    // Dictionary mapping neighbor configuration to display tile atlas coordinates
    private readonly Dictionary<Tuple<DungeonTileType, DungeonTileType, DungeonTileType, DungeonTileType>, Vector2I> neighboursToAtlasCoord = new() {
        // Map configurations of (topLeft, topRight, botLeft, botRight) to display tiles
        {new (DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Floor), new Vector2I(2, 1)}, // All corners Floor
        {new (DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Floor), new Vector2I(1, 3)}, // Outer bottom-right corner
        {new (DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Wall), new Vector2I(0, 0)}, // Outer bottom-left corner
        {new (DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Wall), new Vector2I(0, 2)}, // Outer top-right corner
        {new (DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Wall), new Vector2I(3, 3)}, // Outer top-left corner
        {new (DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Floor), new Vector2I(1, 0)}, // Right edge
        {new (DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Wall), new Vector2I(3, 2)}, // Left edge
        {new (DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Floor), new Vector2I(3, 0)}, // Bottom edge
        {new (DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Wall), new Vector2I(1, 2)}, // Top edge
        {new (DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Floor), new Vector2I(1, 1)}, // Inner bottom-right corner
        {new (DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Floor), new Vector2I(2, 0)}, // Inner bottom-left corner
        {new (DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Floor), new Vector2I(2, 2)}, // Inner top-right corner
        {new (DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Wall), new Vector2I(3, 1)}, // Inner top-left corner
        {new (DungeonTileType.Wall, DungeonTileType.Floor, DungeonTileType.Floor, DungeonTileType.Wall), new Vector2I(2, 3)}, // Bottom-left top-right corners
        {new (DungeonTileType.Floor, DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Floor), new Vector2I(0, 1)}, // Top-left down-right corners
        {new (DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Wall, DungeonTileType.Wall), new Vector2I(0, 3)}, // All corners Wall
    };
    
    public void UpdateDisplayTiles(TileMapLayer worldTileMap, TileMapLayer displayTileMap)
    {
        // Clear existing display tiles
        displayTileMap.Clear();
        
        // For each world tile, update the 4 surrounding display tiles
        // Fixed: GetUsedCells doesn't take a layer parameter
        var usedCells = worldTileMap.GetUsedCells();
        foreach (Vector2I worldCoord in usedCells)
        {
            SetDisplayTilesForWorldTile(worldCoord, worldTileMap, displayTileMap);
        }
        
        GD.Print("Display tiles updated");
    }
    
    private void SetDisplayTilesForWorldTile(Vector2I worldPos, TileMapLayer worldTileMap, TileMapLayer displayTileMap)
    {
        // For each world tile, update 4 display tiles
        foreach (Vector2I offset in DISPLAY_NEIGHBORS)
        {
            Vector2I displayPos = worldPos + offset;
            
            // Calculate the appropriate display tile
            Vector2I atlasCoord = CalculateDisplayTile(displayPos, worldTileMap);
            
            // Set the display tile - Fixed: SetCell needs coordinates first, then source ID (0), then atlas coords
            displayTileMap.SetCell(displayPos, 0, atlasCoord);
        }
    }
    
    private Vector2I CalculateDisplayTile(Vector2I displayPos, TileMapLayer worldTileMap)
    {
        // Each display tile looks at 4 world tiles that surround it
        // These are at relative positions: (0,0), (-1,0), (0,-1), (-1,-1) from the display position
        DungeonTileType botRight = GetWorldTileType(displayPos, worldTileMap);
        DungeonTileType botLeft = GetWorldTileType(displayPos - new Vector2I(1, 0), worldTileMap);
        DungeonTileType topRight = GetWorldTileType(displayPos - new Vector2I(0, 1), worldTileMap);
        DungeonTileType topLeft = GetWorldTileType(displayPos - new Vector2I(1, 1), worldTileMap);
        
        // Create lookup key for the tile configuration
        var key = new Tuple<DungeonTileType, DungeonTileType, DungeonTileType, DungeonTileType>(
            topLeft, topRight, botLeft, botRight);
        
        // Look up the atlas coordinate
        if (neighboursToAtlasCoord.ContainsKey(key))
        {
            return neighboursToAtlasCoord[key];
        }
        
        // Default if no mapping exists
        GD.PrintErr($"No display tile mapping for {topLeft}, {topRight}, {botLeft}, {botRight}");
        return new Vector2I(2, 1); // Default to all-floor tile
    }
    
    public DungeonTileType GetWorldTileType(Vector2I worldPos, TileMapLayer worldTileMap)
    {
        // Check if a cell exists at these coordinates
        if (worldTileMap.GetCellSourceId(worldPos) == -1)
        {
            // No cell, consider it as None/out of bounds
            return DungeonTileType.Wall; // Treat out-of-bounds as Wall for dungeon edges
        }
        
        // Get atlas coordinates for this world tile
        Vector2I atlasCoord = worldTileMap.GetCellAtlasCoords(worldPos);
        
        // Determine tile type based on atlas coordinates
        if (atlasCoord == FloorAtlasCoord)
            return DungeonTileType.Floor;
        else if (atlasCoord == WallAtlasCoord)
            return DungeonTileType.Wall;
        else
        {
            GD.PrintErr($"Unknown tile type at {worldPos} with atlas coord {atlasCoord}");
            return DungeonTileType.Wall; // Default to Wall
        }
    }
}
