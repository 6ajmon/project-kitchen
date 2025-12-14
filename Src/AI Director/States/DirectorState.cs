using Godot;
using System;
using System.Collections.Generic;

public abstract partial class DirectorState : Node
{
    [Export] protected Director Director;
    protected List<DirectorAction> AvailableActions = new List<DirectorAction>();

    public override void _Ready()
    {
        if (Director == null) Director = GetNode<Director>("../../");
    }

    public virtual void Initialize(Director director, List<DirectorAction> actions)
    {
        Director = director;
        AvailableActions = actions;
    }

    public virtual void Enter() { }
    public virtual void Exit() { }
    public abstract void UpdateState(double delta);
}
