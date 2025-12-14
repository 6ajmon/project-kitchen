using Godot;
using System;
using System.Linq;

public partial class Intensify : DirectorState
{
    public override void UpdateState(double delta)
    {
        if (Director == null) return;

        // Accumulate Negative Points
        Director.AddNegativePoints((float)delta * Director.PointsPerSecond);

        // Try to spend points on harmful actions
        TrySpendPoints();
    }

    private void TrySpendPoints()
    {
        // Find affordable actions that hurt the player
        var harmfulActions = AvailableActions.Where(a => a is SpawnHorde || a is BuffEnemies || a is IncreaseSpawnRate).ToList();
        
        // Simple strategy: Buy the most expensive affordable action
        var affordableAction = harmfulActions
            .Where(a => a.Cost <= Director.NegativeActionPoints)
            .OrderByDescending(a => a.Cost)
            .FirstOrDefault();

        if (affordableAction != null)
        {
            Director.SpendNegativePoints(affordableAction.Cost);
            affordableAction.Execute();
        }
    }
}
