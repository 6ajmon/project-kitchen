using Godot;
using System;

public enum ActionType
{
    Benefit,    // Helps the player (spends BenefitPoints)
    Negative    // Hurts the player (spends NegativePoints)
}

public abstract partial class DirectorAction : Node
{
    [Export] public ActionType ActionType = ActionType.Negative;
    [Export] public float Cost = 10.0f;
    [Export] public float Weight = 1.0f; // Higher weight = higher chance to be picked
    [Export] public float Cooldown = 5.0f;

    private double _lastUsedTime = -999.0;

    public bool CanExecute(float availablePoints, double currentTime)
    {
        if (availablePoints < Cost) return false;
        if (currentTime < _lastUsedTime + Cooldown) return false;
        return true;
    }
    
    public void Execute(double currentTime)
    {
        _lastUsedTime = currentTime;
        SignalManager.Instance.EmitSignal(nameof(SignalManager.DirectorActionExecuted), Name, Cost);
        OnExecute();
    }

    protected abstract void OnExecute();
}
