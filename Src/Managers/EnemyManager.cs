using Godot;
using System;
using System.Collections.Generic;

public partial class EnemyManager : Node
{
    public static EnemyManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<EnemyManager>("EnemyManager");

    private List<Enemy> _activeEnemies = new List<Enemy>();

    public void RegisterEnemy(Enemy enemy)
    {
        if (!_activeEnemies.Contains(enemy))
        {
            _activeEnemies.Add(enemy);
        }
    }

    public void UnregisterEnemy(Enemy enemy)
    {
        if (_activeEnemies.Contains(enemy))
        {
            _activeEnemies.Remove(enemy);
        }
    }

    public void BuffAllEnemies(float percentage)
    {
        GD.Print($"[EnemyManager] Buffing {_activeEnemies.Count} enemies by {percentage * 100}%");
        foreach (var enemy in _activeEnemies)
        {
            if (IsInstanceValid(enemy))
            {
                enemy.ApplyBuff(percentage);
            }
        }
    }
}
