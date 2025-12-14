using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class DifficultyStateMachine : Node
{
    public enum State { Maintain, Reduce, Intensify }

    private DirectorState _currentStateNode;
    [Export] private DirectorState[] _stateNodes;
    private Dictionary<State, DirectorState> _states = new Dictionary<State, DirectorState>();

    public void Initialize(Director director, List<DirectorAction> actions)
    {
        _stateNodes = _stateNodes.ToArray();
        _states[State.Maintain] = _stateNodes.FirstOrDefault(s => s is Maintain);
        _states[State.Reduce] = _stateNodes.FirstOrDefault(s => s is Reduce);
        _states[State.Intensify] = _stateNodes.FirstOrDefault(s => s is Intensify);
        foreach (var state in _states.Values)
        {
            state.Initialize(director, actions);
        }

        TransitionTo(State.Maintain);
    }

    public void TransitionTo(State newState)
    {
        if (!_states.ContainsKey(newState))
        {
            GD.PrintErr($"[DifficultyStateMachine] State {newState} not found!");
            return;
        }

        _currentStateNode?.Exit();
        _currentStateNode = _states[newState];
        _currentStateNode.Enter();
        
        GD.Print($"[DifficultyStateMachine] Transitioned to {newState}");
        SignalManager.Instance.EmitSignal(nameof(SignalManager.DifficultyStateChanged), newState.ToString());
    }

    public override void _Process(double delta)
    {
        _currentStateNode?.UpdateState(delta);
    }
}
