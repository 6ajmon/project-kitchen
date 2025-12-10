using Godot;
using System;

public partial class Director : Node
{
    [Export] public DirectorData Data { get; private set; }

    [ExportCategory("DDA Settings")]
    [Export] public float CheckInterval = 5.0f;
    [Export] public Curve ExpectedPerformanceCurve;
    [Export] public float ExpectedSessionDuration = 600.0f; // 10 minutes for curve normalization

    [ExportGroup("Performance Weights")]
    [Export] public float WeightKills = 10.0f;
    [Export] public float WeightHealth = 50.0f; // High weight for staying healthy
    [Export] public float WeightDamageTaken = 2.0f;

    [ExportGroup("Flow Thresholds")]
    [Export] public float AnxietyThreshold = 0.8f;
    [Export] public float BoredomThreshold = 1.2f;

    public enum GameState { Maintain, EaseUp, Intensify }
    public GameState CurrentState { get; private set; } = GameState.Maintain;

    private Timer _checkTimer;

    public override void _Ready()
    {
        if (Data == null)
        {
            Data = GetNodeOrNull<DirectorData>("DirectorData");
            if (Data == null)
            {
                // Fallback: Try to find it in children or create it (though creating it might not link to the right data sources)
                Data = GetChildOrNull<DirectorData>(0);
            }
        }

        _checkTimer = new Timer();
        _checkTimer.WaitTime = CheckInterval;
        _checkTimer.OneShot = false;
        _checkTimer.Timeout += OnCheckTimerTimeout;
        AddChild(_checkTimer);
        _checkTimer.Start();
    }

    private void OnCheckTimerTimeout()
    {
        EvaluatePerformance();
    }

    private void EvaluatePerformance()
    {
        if (Data == null || Data.Performance == null || Data.Player == null) return;

        float time = (float)Data.Performance.TotalSessionTime;
        float expectedPerformance = CalculateExpectedPerformance(time);
        float currentPerformance = CalculateCurrentPerformance();

        // Avoid division by zero
        if (expectedPerformance <= 0.001f) expectedPerformance = 0.001f;

        float flowRatio = currentPerformance / expectedPerformance;

        UpdateGameState(flowRatio);
    }

    private float CalculateExpectedPerformance(float time)
    {
        // Ep[t]
        if (ExpectedPerformanceCurve != null)
        {
            float normalizedTime = Mathf.Clamp(time / ExpectedSessionDuration, 0f, 1f);
            // Assuming the curve Y value is the expected score multiplier or raw value
            // Let's assume curve Y is 0-100 range representing score
            return ExpectedPerformanceCurve.Sample(normalizedTime) * 100f; 
        }
        
        // Fallback linear formula: e.g., 10 points per minute
        return 10.0f * (time / 60.0f) + 10.0f; // Base 10
    }

    private float CalculateCurrentPerformance()
    {
        // Cp = (w1 * kills) + (w2 * health_percentage) - (w3 * damage_taken)
        
        float kills = Data.Performance.TotalKills;
        float healthPct = Data.Player.MaxHealth > 0 ? (Data.Player.CurrentHealth / Data.Player.MaxHealth) : 0;
        float damageTaken = Data.Performance.TotalDamageTaken;

        float score = (WeightKills * kills) + (WeightHealth * healthPct) - (WeightDamageTaken * damageTaken);
        return Mathf.Max(1.0f, score); // Ensure at least 1 to avoid 0/x issues or negative ratios
    }

    private void UpdateGameState(float ratio)
    {
        GameState previousState = CurrentState;

        if (ratio < AnxietyThreshold)
        {
            // Player is struggling (Performance < Expected) -> Anxiety -> Make it easier
            SetState(GameState.EaseUp);
        }
        else if (ratio > BoredomThreshold)
        {
            // Player is doing too well (Performance > Expected) -> Boredom -> Make it harder
            SetState(GameState.Intensify);
        }
        else
        {
            // Flow Channel
            SetState(GameState.Maintain);
        }
        
        if (previousState != CurrentState)
        {
            GD.Print($"DDA State Change: {previousState} -> {CurrentState} (Ratio: {ratio:F2})");
        }
    }

    private void SetState(GameState newState)
    {
        CurrentState = newState;
        switch (CurrentState)
        {
            case GameState.EaseUp:
                ReduceSpawnRate();
                break;
            case GameState.Intensify:
                SpawnHorde();
                BuffEnemies();
                break;
            case GameState.Maintain:
                // Keep current parameters
                break;
        }
    }

    // Dummy Actions
    private void SpawnHorde()
    {
        GD.Print("Director Action: Spawning Horde!");
    }

    private void BuffEnemies()
    {
        GD.Print("Director Action: Buffing Enemies!");
    }

    private void ReduceSpawnRate()
    {
        GD.Print("Director Action: Reducing Spawn Rate!");
    }
}
