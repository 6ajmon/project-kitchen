using Godot;
using System;

public partial class IncreaseSpawnRate : DirectorAction
{
    protected override void OnExecute()
    {
        GD.Print($"[Director] Increasing Spawn Rate! (Cost: {Cost})");
        // Implement logic here
    }
}
