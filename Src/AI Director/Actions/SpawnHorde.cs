using Godot;
using System;

public partial class SpawnHorde : DirectorAction
{
    [Export] public int HordeSize = 10;

    protected override void OnExecute()
    {
        GD.Print($"[Director] Spawning Horde of {HordeSize}! (Cost: {Cost})");
        for (int i = 0; i < HordeSize; i++)
        {
            // Force spawn immediately
            EnemyManager.Instance.ForceSpawnEnemy();
        }
    }
}
