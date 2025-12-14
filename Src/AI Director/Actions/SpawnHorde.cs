using Godot;
using System;

public partial class SpawnHorde : DirectorAction
{
    protected override void OnExecute()
    {
        GD.Print($"[Director] Spawning Horde! (Cost: {Cost})");
        // Implement actual spawning logic here
    }
}
