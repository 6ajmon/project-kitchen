using Godot;
using System;
using System.Linq;

public partial class Reduce : DirectorState
{
    public override void UpdateState(double delta)
    {
        if (Director == null) return;

        // Calculate points based on how far below Flow threshold
        float multiplier = Director.GetPointsMultiplier();
        float pointsToAdd = (float)delta * Director.BasePointsPerSecond * multiplier;
        
        // In Reduce state, we accumulate Benefit Points
        Director.AddBenefitPoints(pointsToAdd);

        // And spend them to help the player recover
        Director.TrySpendBenefitPoints();
    }
}
