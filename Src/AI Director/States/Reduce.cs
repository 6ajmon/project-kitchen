using Godot;
using System;
using System.Linq;

public partial class Reduce : DirectorState
{
    public override void UpdateState(double delta)
    {
        if (Director == null) return;

        // Accumulate Benefit Points
        Director.AddBenefitPoints((float)delta * Director.PointsPerSecond);

        // Try to spend points on helpful actions
        TrySpendPoints();
    }

    private void TrySpendPoints()
    {
        // Find affordable actions that help the player (e.g., ReduceSpawnRate, DebuffEnemies)
        // For now, we filter by type or name, or we could have a tag system.
        // Let's assume we look for specific types.
        
        var helpfulActions = AvailableActions.Where(a => a is ReduceSpawnRate || a is DebuffEnemies).ToList();
        
        // Simple strategy: Buy the most expensive affordable action
        var affordableAction = helpfulActions
            .Where(a => a.Cost <= Director.BenefitActionPoints)
            .OrderByDescending(a => a.Cost)
            .FirstOrDefault();

        if (affordableAction != null)
        {
            Director.SpendBenefitPoints(affordableAction.Cost);
            affordableAction.Execute();
        }
    }
}
