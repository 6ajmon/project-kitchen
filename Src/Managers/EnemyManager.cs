using Godot;
using System;
using System.Collections.Generic;

public partial class EnemyManager : Node
{
    public static EnemyManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<EnemyManager>("EnemyManager");

    [Export] public Godot.Collections.Array<PackedScene> EnemyScenes = new Godot.Collections.Array<PackedScene>();
    
    [ExportGroup("Spawn Timing")]
    [Export] public float BaseSpawnInterval = 3.0f;     // Starting spawn interval (easier)
    [Export] public float MinSpawnInterval = 0.5f;      // Fastest spawn interval (hardest)
    
    private float _currentSpawnInterval;

    [ExportGroup("Spawn Location")]
    [Export] public float MinSpawnRadius = 300.0f;
    [Export] public float MaxSpawnRadius = 600.0f;
    
    [ExportGroup("Limits")]
    [Export] public int BaseMaxEnemies = 10;            // Starting max enemies
    [Export] public int MaxMaxEnemies = 30;             // Maximum enemies at hardest point
    [Export] public bool SpawningEnabled = true;

    private int _currentMaxEnemies;
    private List<Enemy> _activeEnemies = new List<Enemy>();
    private TileMapLayer _worldTileMap;
    private Vector2I _floorAtlasCoord;
    private Vector2I _wallAtlasCoord;
    private Timer _spawnTimer;
    private RandomNumberGenerator _rng = new RandomNumberGenerator();
    
    // Director action modifiers (decay over time)
    private float _spawnRateMultiplier = 1.0f;
    private const float MultiplierDecayRate = 0.1f; // Return to 1.0 over time

    public override void _Ready()
    {
        _currentSpawnInterval = BaseSpawnInterval;
        _currentMaxEnemies = BaseMaxEnemies;
        
        _spawnTimer = new Timer();
        _spawnTimer.WaitTime = _currentSpawnInterval;
        _spawnTimer.Timeout += OnSpawnTimerTimeout;
        AddChild(_spawnTimer);
        _spawnTimer.Start();
    }

    public override void _Process(double delta)
    {
        UpdateDifficultyScaling();
        DecayMultiplier((float)delta);
    }
    
    private void DecayMultiplier(float delta)
    {
        // Slowly return multiplier to 1.0
        _spawnRateMultiplier = Mathf.MoveToward(_spawnRateMultiplier, 1.0f, MultiplierDecayRate * delta);
    }

    /// <summary>
    /// Updates spawn interval and max enemies based on GameManager's difficulty curve.
    /// </summary>
    private void UpdateDifficultyScaling()
    {
        float curveValue = GameManager.Instance?.CurrentDifficulty ?? 0f;
        
        // Interpolate spawn interval (higher curve = faster spawns = lower interval)
        float baseInterval = Mathf.Lerp(BaseSpawnInterval, MinSpawnInterval, curveValue);
        
        // Apply Director action multiplier
        _currentSpawnInterval = baseInterval * _spawnRateMultiplier;
        _currentSpawnInterval = Mathf.Clamp(_currentSpawnInterval, MinSpawnInterval * 0.5f, BaseSpawnInterval * 2.0f);
        _spawnTimer.WaitTime = _currentSpawnInterval;
        
        // Interpolate max enemies (higher curve = more enemies allowed)
        _currentMaxEnemies = Mathf.RoundToInt(Mathf.Lerp(BaseMaxEnemies, MaxMaxEnemies, curveValue));
    }

    /// <summary>
    /// Get current spawn interval (for Director actions to modify).
    /// </summary>
    public float GetCurrentSpawnInterval() => _currentSpawnInterval;
    
    /// <summary>
    /// Modify spawn rate via multiplier (used by Director actions).
    /// Multiplier > 1 = slower spawns, Multiplier < 1 = faster spawns.
    /// Decays back to 1.0 over time.
    /// </summary>
    public void ModifySpawnRate(float multiplier)
    {
        _spawnRateMultiplier *= multiplier;
        _spawnRateMultiplier = Mathf.Clamp(_spawnRateMultiplier, 0.3f, 3.0f);
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
        
        if (_activeEnemies.Count >= _currentMaxEnemies) 
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

    public Enemy ForceSpawnEnemy()
    {
        if (_worldTileMap == null || GameManager.Instance.Player == null) 
        {
            GD.Print("[EnemyManager] Cannot force spawn: Missing dependencies.");
            return null;
        }
        return TrySpawnEnemy();
    }

    private Enemy TrySpawnEnemy()
    {
        if (EnemyScenes.Count == 0) 
        {
            GD.Print("[EnemyManager] No EnemyScenes assigned!");
            return null;
        }

        // Pick random enemy
        var scene = EnemyScenes[_rng.RandiRange(0, EnemyScenes.Count - 1)];
        
        // Find valid position
        Vector2 playerPos = GameManager.Instance.Player.GlobalPosition;
        Enemy spawnedEnemy = null;

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
                     spawnedEnemy = SpawnEnemy(scene, spawnPos);
                     break;
                }
                // else { GD.Print($"[EnemyManager] Tile at {cellPos} is not floor. Atlas: {atlasCoord}, Expected: {_floorAtlasCoord}"); }
            }
            // else { GD.Print($"[EnemyManager] No tile at {cellPos}"); }
        }

        if (spawnedEnemy == null)
        {
            // GD.Print("[EnemyManager] Failed to find valid spawn position after 10 tries.");
        }
        return spawnedEnemy;
    }

    private Enemy SpawnEnemy(PackedScene scene, Vector2 position)
    {
        var enemy = scene.Instantiate<Enemy>();
        enemy.GlobalPosition = position;
        GetTree().Root.AddChild(enemy); 
        // Registration happens in Enemy._Ready()
        return enemy;
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

    public void DebuffAllEnemies(float percentage)
    {
        GD.Print($"[EnemyManager] Debuffing {_activeEnemies.Count} enemies by {percentage * 100}%");
        foreach (var enemy in _activeEnemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ApplyDebuff(percentage);
            }
        }
    }
}
