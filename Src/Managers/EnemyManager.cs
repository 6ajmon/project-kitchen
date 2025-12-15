using Godot;
using System;
using System.Collections.Generic;

public partial class EnemyManager : Node
{
    public static EnemyManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<EnemyManager>("EnemyManager");

    [Export] public Godot.Collections.Array<PackedScene> EnemyScenes = new Godot.Collections.Array<PackedScene>();
    
    private float _spawnInterval = 2.0f;
    [Export] public float SpawnInterval 
    { 
        get => _spawnInterval; 
        set 
        { 
            _spawnInterval = value; 
            if (_spawnTimer != null) _spawnTimer.WaitTime = _spawnInterval; 
        } 
    }

    [Export] public float MinSpawnRadius = 300.0f;
    [Export] public float MaxSpawnRadius = 600.0f;
    [Export] public int MaxEnemies = 50;
    [Export] public bool SpawningEnabled = true;

    private List<Enemy> _activeEnemies = new List<Enemy>();
    private TileMapLayer _worldTileMap;
    private Vector2I _floorAtlasCoord;
    private Vector2I _wallAtlasCoord;
    private Timer _spawnTimer;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();

    public override void _Ready()
    {
        _spawnTimer = new Timer();
        _spawnTimer.WaitTime = SpawnInterval;
        _spawnTimer.Timeout += OnSpawnTimerTimeout;
        AddChild(_spawnTimer);
        _spawnTimer.Start();
    }

    public void SetWorldTileMap(TileMapLayer tileMap, Vector2I floorCoord, Vector2I wallCoord)
    {
        _worldTileMap = tileMap;
        _floorAtlasCoord = floorCoord;
        _wallAtlasCoord = wallCoord;
        GD.Print("[EnemyManager] WorldTileMap and coords set.");
    }

    private void OnSpawnTimerTimeout()
    {
        if (!SpawningEnabled) return;
        
        if (_activeEnemies.Count >= MaxEnemies) 
        {
            // GD.Print("[EnemyManager] Max enemies reached.");
            return;
        }
        
        if (_worldTileMap == null)
        {
            GD.Print("[EnemyManager] WorldTileMap is null.");
            return;
        }
        
        if (GameManager.Instance.Player == null)
        {
            GD.Print("[EnemyManager] Player is null.");
            return;
        }

        TrySpawnEnemy();
    }

    public void ForceSpawnEnemy()
    {
        if (_worldTileMap == null || GameManager.Instance.Player == null) 
        {
            GD.Print("[EnemyManager] Cannot force spawn: Missing dependencies.");
            return;
        }
        TrySpawnEnemy();
    }

    private void TrySpawnEnemy()
    {
        if (EnemyScenes.Count == 0) 
        {
            GD.Print("[EnemyManager] No EnemyScenes assigned!");
            return;
        }

        // Pick random enemy
        var scene = EnemyScenes[_rng.RandiRange(0, EnemyScenes.Count - 1)];
        
        // Find valid position
        Vector2 playerPos = GameManager.Instance.Player.GlobalPosition;
        bool spawned = false;

        for (int i = 0; i < 10; i++) // Try 10 times
        {
            float angle = _rng.RandfRange(0, Mathf.Tau);
            float dist = _rng.RandfRange(MinSpawnRadius, MaxSpawnRadius);
            Vector2 offset = Vector2.FromAngle(angle) * dist;
            Vector2 spawnPos = playerPos + offset;

            Vector2I cellPos = _worldTileMap.LocalToMap(_worldTileMap.ToLocal(spawnPos));
            
            // Check if cell exists
            int sourceId = _worldTileMap.GetCellSourceId(cellPos);
            if (sourceId != -1)
            {
                // Check if it is a floor tile using atlas coordinates
                Vector2I atlasCoord = _worldTileMap.GetCellAtlasCoords(cellPos);
                if (atlasCoord == _floorAtlasCoord)
                {
                     SpawnEnemy(scene, spawnPos);
                     spawned = true;
                     break;
                }
                // else { GD.Print($"[EnemyManager] Tile at {cellPos} is not floor. Atlas: {atlasCoord}, Expected: {_floorAtlasCoord}"); }
            }
            // else { GD.Print($"[EnemyManager] No tile at {cellPos}"); }
        }

        if (!spawned)
        {
            // GD.Print("[EnemyManager] Failed to find valid spawn position after 10 tries.");
        }
    }

    private void SpawnEnemy(PackedScene scene, Vector2 position)
    {
        var enemy = scene.Instantiate<Enemy>();
        enemy.GlobalPosition = position;
        GetTree().Root.AddChild(enemy); 
        // Registration happens in Enemy._Ready()
    }

    public void RegisterEnemy(Enemy enemy)
    {
        if (!_activeEnemies.Contains(enemy))
        {
            _activeEnemies.Add(enemy);
        }
    }

    public void UnregisterEnemy(Enemy enemy)
    {
        if (_activeEnemies.Contains(enemy))
        {
            _activeEnemies.Remove(enemy);
        }
    }

    public void BuffAllEnemies(float percentage)
    {
        GD.Print($"[EnemyManager] Buffing {_activeEnemies.Count} enemies by {percentage * 100}%");
        foreach (var enemy in _activeEnemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ApplyBuff(percentage);
            }
        }
    }
}
