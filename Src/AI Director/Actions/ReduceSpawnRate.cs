using Godot;
using System;

public partial class ReduceSpawnRate : DirectorAction
{
    [Export] public float RateMultiplier = 1.25f; // Increase interval by 25%

    public ReduceSpawnRate()
    {
        ActionType = ActionType.Benefit;
    }

    protected override void OnExecute()
    {
        GD.Print($"[Director] Reducing Spawn Rate! (Cost: {Cost})");
        EnemyManager.Instance.SpawnInterval *= RateMultiplier;
        // Clamp to some maximum
        EnemyManager.Instance.SpawnInterval = Mathf.Min(10.0f, EnemyManager.Instance.SpawnInterval);
    }
}
