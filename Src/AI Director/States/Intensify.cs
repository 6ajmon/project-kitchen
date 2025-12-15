using Godot;
using System;
using System.Linq;

public partial class Intensify : DirectorState
{
    public override void UpdateState(double delta)
    {
        if (Director == null) return;

        // In Intensify state, we accumulate Negative Points
        Director.AddNegativePoints((float)delta * Director.PointsPerSecond);

        // And aggressively spend them to challenge the player
        Director.TrySpendNegativePoints();
    }
}
