using Godot;
using System;

public partial class IncreaseSpawnRate : DirectorAction
{
    [Export] public float RateMultiplier = 0.7f; // Speed up spawns by 30%

    protected override void OnExecute()
    {
        GD.Print($"[Director] Increasing Spawn Rate! (Multiplier: {RateMultiplier}x, Cost: {Cost})");
        EnemyManager.Instance.ModifySpawnRate(RateMultiplier);
    }
}
