using Godot;
using System;

public partial class DebuffEnemies : DirectorAction
{
    [Export] public float DebuffPercentage = 0.05f; // 5%

    public DebuffEnemies()
    {
        ActionType = ActionType.Benefit;
    }

    protected override void OnExecute()
    {
        GD.Print($"[Director] Debuffing Enemies by {DebuffPercentage*100}%! (Cost: {Cost})");
        EnemyManager.Instance.DebuffAllEnemies(DebuffPercentage);
    }
}
