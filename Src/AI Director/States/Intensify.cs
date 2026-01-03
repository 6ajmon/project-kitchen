using Godot;
using System;
using System.Linq;

public partial class Intensify : DirectorState
{
    public override void UpdateState(double delta)
    {
        if (Director == null) return;

        // Calculate points based on how far above Flow threshold
        float multiplier = Director.GetPointsMultiplier();
        float pointsToAdd = (float)delta * Director.BasePointsPerSecond * multiplier;
        
        // In Intensify state, we accumulate Negative Points
        Director.AddNegativePoints(pointsToAdd);

        // And aggressively spend them to challenge the player
        Director.TrySpendNegativePoints();
    }
}
