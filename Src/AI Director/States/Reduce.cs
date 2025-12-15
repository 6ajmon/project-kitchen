using Godot;
using System;
using System.Linq;

public partial class Reduce : DirectorState
{
    public override void UpdateState(double delta)
    {
        if (Director == null) return;

        // In Reduce state, we accumulate Benefit Points
        Director.AddBenefitPoints((float)delta * Director.PointsPerSecond);

        // And spend them to help the player recover
        Director.TrySpendBenefitPoints();
    }
}
