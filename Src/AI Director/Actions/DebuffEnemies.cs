using Godot;
using System;

public partial class DebuffEnemies : DirectorAction
{
    protected override void OnExecute()
    {
        GD.Print($"[Director] Debuffing Enemies! (Cost: {Cost})");
        // Implement debuff logic here
    }
}
