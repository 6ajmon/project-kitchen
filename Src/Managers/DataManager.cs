using Godot;
using System;

public partial class DataManager : Node
{
    public static DataManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<DataManager>("DataManager");

    public DirectorData DirectorData { get; private set; }
    public Player Player { get; private set; }

    public float TotalSpawnedValue { get; private set; }
    public float CurrentLivingValue { get; private set; }

    public override void _Ready()
    {
        // Subscribe to gameplay signals
        SignalManager.Instance.EnemyKilled += OnEnemyKilled;
        SignalManager.Instance.PlayerTookDamage += OnPlayerTookDamage;
        SignalManager.Instance.PlayerHealed += OnPlayerHealed;
        SignalManager.Instance.WeaponFired += OnWeaponFired;
        SignalManager.Instance.WeaponHit += OnWeaponHit;
    }

    public override void _ExitTree()
    {
        if (SignalManager.Instance != null)
        {
            SignalManager.Instance.EnemyKilled -= OnEnemyKilled;
            SignalManager.Instance.PlayerTookDamage -= OnPlayerTookDamage;
            SignalManager.Instance.PlayerHealed -= OnPlayerHealed;
            SignalManager.Instance.WeaponFired -= OnWeaponFired;
            SignalManager.Instance.WeaponHit -= OnWeaponHit;
        }
    }

    public override void _Process(double delta)
    {
        if (DirectorData?.Performance != null)
        {
            DirectorData.Performance.TotalSessionTime += delta;
            DirectorData.Performance.TimeSinceLastHit += delta;
        }
        
        // Sync Player health to PlayerData
        SyncPlayerHealth();
    }
    
    private void SyncPlayerHealth()
    {
        if (DirectorData?.Player == null)
        {
            // GD.Print("[DataManager] DirectorData.Player is null");
            return;
        }
        if (Player == null)
        {
            // GD.Print("[DataManager] Player is null");
            return;
        }
        
        var healthComponent = Player.GetNodeOrNull<HealthComponent>("HealthComponent");
        if (healthComponent != null)
        {
            DirectorData.Player.CurrentHealth = healthComponent.CurrentHealth;
            DirectorData.Player.MaxHealth = healthComponent.MaxHealth;
            // GD.Print($"[DataManager] Synced health: {healthComponent.CurrentHealth}/{healthComponent.MaxHealth}");
        }
        else
        {
            GD.Print("[DataManager] HealthComponent not found on Player!");
        }
    }

    public void RegisterDirectorData(DirectorData data)
    {
        DirectorData = data;
        GD.Print("[DataManager] DirectorData registered.");
    }

    public void RegisterPlayer(Player player)
    {
        Player = player;
        GD.Print("[DataManager] Player registered.");
    }

    public void RegisterEnemySpawn(float value)
    {
        TotalSpawnedValue += value;
        CurrentLivingValue += value;
        DirectorData?.Performance?.RegisterEnemySpawn(value);
    }

    public void RegisterEnemyDeath(float value)
    {
        CurrentLivingValue = Mathf.Max(0, CurrentLivingValue - value);
        DirectorData?.Performance?.RegisterEnemyKill(value);
    }

    private void OnEnemyKilled()
    {
        // Handled in RegisterEnemyDeath now, but keeping for signal consistency if needed
    }

    private void OnPlayerTookDamage(float amount)
    {
        if (DirectorData?.Player != null)
        {
            DirectorData.Player.CurrentHealth = Mathf.Clamp(DirectorData.Player.CurrentHealth - amount, 0, DirectorData.Player.MaxHealth);
        }

        if (DirectorData?.Performance != null)
        {
            DirectorData.Performance.RegisterDamageTaken(amount);
            DirectorData.Performance.TimeSinceLastHit = 0;
        }
    }

    private void OnPlayerHealed(float amount)
    {
        if (DirectorData?.Player != null)
        {
            DirectorData.Player.CurrentHealth = Mathf.Clamp(DirectorData.Player.CurrentHealth + amount, 0, DirectorData.Player.MaxHealth);
        }
    }

    private void OnWeaponFired()
    {
        if (DirectorData?.Performance != null)
        {
            DirectorData.Performance.ShotsFired++;
        }
    }

    private void OnWeaponHit()
    {
        if (DirectorData?.Performance != null)
        {
            DirectorData.Performance.ShotsHit++;
        }
    }
}
