using Godot;
using System;

public partial class GameManager : Node
{
    public static GameManager Instance => ((SceneTree)Engine.GetMainLoop()).Root.GetNode<GameManager>("GameManager");

    [ExportCategory("Session Settings")]
    [Export] public float SessionDuration = 120.0f;  // Total session length in seconds
    
    private bool _showDebugUi = true;
    [Export] public bool ShowDebugUi 
    { 
        get => _showDebugUi;
        set 
        {
            _showDebugUi = value;
            if (IsInsideTree())
            {
                SignalManager.Instance.EmitSignal(SignalManager.SignalName.DebugUiVisibilityChanged, value);
            }
        }
    }
    
    [ExportGroup("Difficulty Curve")]
    [Export] public Curve DifficultyCurve;           // Shared curve: X = time progress (0-1), Y = difficulty (0-1)
    
    public Player Player { get; private set; }
    public float SessionTime { get; private set; } = 0.0f;
    
    /// <summary>
    /// Returns normalized session progress (0 at start, 1 at SessionDuration).
    /// </summary>
    public float SessionProgress => Mathf.Clamp(SessionTime / SessionDuration, 0f, 1f);
    
    /// <summary>
    /// Returns difficulty value from curve at current time (0-1).
    /// Falls back to linear if no curve assigned.
    /// </summary>
    public float CurrentDifficulty
    {
        get
        {
            if (DifficultyCurve != null)
            {
                return DifficultyCurve.Sample(SessionProgress);
            }
            return SessionProgress; // Linear fallback
        }
    }

    public override void _Process(double delta)
    {
        SessionTime += (float)delta;
    }

    public void RegisterPlayer(Player player)
    {
        Player = player;
        GD.Print("[GameManager] Player registered.");
        
        // Also register with DataManager as it was doing before
        DataManager.Instance.RegisterPlayer(player);
    }
}
