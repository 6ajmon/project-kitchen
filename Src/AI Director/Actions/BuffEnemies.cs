using Godot;
using System;

public partial class BuffEnemies : DirectorAction
{
    protected override void OnExecute()
    {
        GD.Print($"[Director] Buffing Enemies! (Cost: {Cost})");
        // Implement buff logic here
    }
}
