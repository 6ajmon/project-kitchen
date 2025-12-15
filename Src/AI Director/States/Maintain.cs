using Godot;
using System;

public partial class Maintain : DirectorState
{
    public override void UpdateState(double delta)
    {
        // In Maintain state, we can choose to do nothing, or spend points slowly/randomly
        // For now, let's allow spending both types but maybe with lower probability or frequency?
        // Or just let the Director accumulate points for later phases.
    }
}
