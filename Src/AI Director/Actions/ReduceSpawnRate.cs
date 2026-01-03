using Godot;
using System;

public partial class ReduceSpawnRate : DirectorAction
{
    [Export] public float RateMultiplier = 1.5f; // Slow down spawns by 50%

    public ReduceSpawnRate()
    {
        ActionType = ActionType.Benefit;
    }

    protected override void OnExecute()
    {
        GD.Print($"[Director] Reducing Spawn Rate! (Multiplier: {RateMultiplier}x, Cost: {Cost})");
        EnemyManager.Instance.ModifySpawnRate(RateMultiplier);
    }
}
