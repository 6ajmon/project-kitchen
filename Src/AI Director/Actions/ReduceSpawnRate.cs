using Godot;
using System;

public partial class ReduceSpawnRate : DirectorAction
{
    protected override void OnExecute()
    {
        GD.Print($"[Director] Reducing Spawn Rate! (Cost: {Cost})");
        // Implement logic here
    }
}
