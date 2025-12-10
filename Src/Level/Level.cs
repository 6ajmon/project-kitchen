using Godot;
using System;

public partial class Level : Node
{
    [Export]
    private PackedScene PlayerScene;

    [Export]
    private PackedScene PlayerCameraScene;

    [Export]
    private PackedScene EnemyRedScene;

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

        // Ensure we have the PlayerCamera scene reference
        if (PlayerCameraScene == null)
        {
            GD.PrintErr("PlayerCamera scene not assigned. Set it in the inspector.");
        }

        // Ensure we have the EnemyRed scene reference
        if (EnemyRedScene == null)
        {
            GD.PrintErr("EnemyRed scene not assigned. Set it in the inspector.");
        }
    }

    // Called when dungeon generation is completed
    private void OnDungeonGenerationCompleted(Vector2I startRoomPosition)
    {
        GD.Print($"Dungeon generation completed. Spawning player at {startRoomPosition}");
        SpawnPlayerAtPosition(startRoomPosition);
        SpawnEnemyAtPosition(startRoomPosition + new Vector2I(60, 0));
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

        // Attach camera to the player if the camera scene is available
        if (PlayerCameraScene != null)
        {
            // Instance the camera scene
            PlayerCamera camera = PlayerCameraScene.Instantiate<PlayerCamera>();

            if (camera == null)
            {
                GD.PrintErr("Failed to instantiate PlayerCamera scene");
                return;
            }

            // Add camera as a child of the player
            _player.AddChild(camera);

            // Make this the current camera
            camera.MakeCurrent();

            GD.Print("PlayerCamera attached to player");
        }
        else
        {
            GD.PrintErr("PlayerCamera scene not assigned, player will not have a camera");
        }
    }

    private void SpawnEnemyAtPosition(Vector2I position)
    {
        if (EnemyRedScene == null)
        {
            GD.PrintErr("Cannot spawn enemy: EnemyRedScene not assigned");
            return;
        }

        EnemyRed enemy = EnemyRedScene.Instantiate<EnemyRed>();

        if (enemy == null)
        {
            GD.PrintErr("Failed to instantiate EnemyRed scene");
            return;
        }

        enemy.Position = new Vector2(position.X, position.Y);

        AddChild(enemy);
        GD.Print($"EnemyRed spawned at position: {position}");
    }
}
