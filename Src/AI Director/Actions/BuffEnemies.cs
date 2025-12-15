using Godot;
using System;

public partial class BuffEnemies : DirectorAction
{
    [Export] public float BuffPercentage = 0.05f; // 5%

    protected override void OnExecute()
    {
        GD.Print($"[Director] Buffing Enemies by {BuffPercentage*100}%! (Cost: {Cost})");
        EnemyManager.Instance.BuffAllEnemies(BuffPercentage);
    }
}
