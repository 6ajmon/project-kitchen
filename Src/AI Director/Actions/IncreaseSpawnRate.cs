using Godot;
using System;

public partial class IncreaseSpawnRate : DirectorAction
{
    [Export] public float RateMultiplier = 0.8f; // Decrease interval by 20%

    protected override void OnExecute()
    {
        GD.Print($"[Director] Increasing Spawn Rate! (Cost: {Cost})");
        EnemyManager.Instance.SpawnInterval *= RateMultiplier;
        // Clamp to some minimum
        EnemyManager.Instance.SpawnInterval = Mathf.Max(0.5f, EnemyManager.Instance.SpawnInterval);
    }
}
