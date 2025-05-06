using Godot;
using System;

public partial class Level : Node2D
{
    [Export]
    private PackedScene PlayerScene;
    
    private DungeonGenerator _dungeonGenerator;
    private Player _player;
    
    public override void _Ready()
    {
        // Find the DungeonGenerator node
        _dungeonGenerator = GetNode<DungeonGenerator>("DungeonGenerator");
        
        if (_dungeonGenerator != null)
        {
            // Connect to the DungeonGenerationCompleted signal
            _dungeonGenerator.DungeonGenerationCompleted += OnDungeonGenerationCompleted;
            GD.Print("Connected to DungeonGenerator's completion signal");
        }
        else
        {
            GD.PrintErr("DungeonGenerator not found. Make sure it's a child of Level with the path 'DungeonGenerator'");
        }
        
        // Ensure we have the Player scene reference
        if (PlayerScene == null)
        {
            GD.PrintErr("Player scene not assigned. Set it in the inspector.");
        }
    }
    
    // Called when dungeon generation is completed
    private void OnDungeonGenerationCompleted(Vector2I startRoomPosition)
    {
        GD.Print($"Dungeon generation completed. Spawning player at {startRoomPosition}");
        SpawnPlayerAtPosition(startRoomPosition);
    }
    
    private void SpawnPlayerAtPosition(Vector2I position)
    {
        if (PlayerScene == null)
        {
            GD.PrintErr("Cannot spawn player: PlayerScene not assigned");
            return;
        }
        
        // Instance the player scene
        _player = PlayerScene.Instantiate<Player>();
        
        if (_player == null)
        {
            GD.PrintErr("Failed to instantiate Player scene");
            return;
        }
        
        // Set player position to the center of the starting room
        _player.Position = new Vector2(position.X, position.Y);
        
        // Add player to the scene
        AddChild(_player);
        GD.Print($"Player spawned at starting room position: {position}");
    }
}
