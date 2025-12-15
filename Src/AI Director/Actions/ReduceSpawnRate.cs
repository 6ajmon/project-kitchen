using Godot;
using System;

public partial class ReduceSpawnRate : DirectorAction
{
    public ReduceSpawnRate()
    {
        ActionType = ActionType.Benefit;
    }

    protected override void OnExecute()
    {
        GD.Print($"[Director] Reducing Spawn Rate! (Cost: {Cost})");
        // Implement logic here
    }
}
