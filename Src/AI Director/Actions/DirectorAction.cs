using Godot;
using System;

public abstract partial class DirectorAction : Node
{
    [Export] public float Cost = 10.0f;
    
    public void Execute()
    {
        SignalManager.Instance.EmitSignal(nameof(SignalManager.DirectorActionExecuted), Name, Cost);
        OnExecute();
    }

    protected abstract void OnExecute();
}
