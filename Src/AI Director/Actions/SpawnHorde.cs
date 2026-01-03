using Godot;
using System;

public partial class SpawnHorde : DirectorAction
{
    [Export] public int HordeSize = 10;

    protected override void OnExecute()
    {
        GD.Print($"[Director] Spawning Horde of {HordeSize}! (Cost: {Cost})");
        float totalDifficulty = 0;
        for (int i = 0; i < HordeSize; i++)
        {
            // Force spawn immediately
            var enemy = EnemyManager.Instance.ForceSpawnEnemy();
            if (enemy != null)
            {
                totalDifficulty += enemy.DifficultyValue;
            }
        }
        
        // Register this as Director-induced spawn so it doesn't count as "Natural Pressure" immediately
        if (DataManager.Instance.DirectorData?.Performance != null)
        {
            DataManager.Instance.DirectorData.Performance.RegisterDirectorSpawn(totalDifficulty);
        }
    }
}
