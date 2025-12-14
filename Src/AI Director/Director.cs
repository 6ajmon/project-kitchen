using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class Director : Node
{
    [Export] public DirectorData Data { get; private set; }
    [Export] public DifficultyStateMachine StateMachine { get; private set; }

    [ExportCategory("DDA Settings")]
    [Export] public float CheckInterval = 1.0f;
    [Export] public Curve ExpectedPerformanceCurve;
    [Export] public float ExpectedSessionDuration = 600.0f;

    [ExportGroup("Performance Weights")]
    [Export] public float WeightKills = 10.0f;
    [Export] public float WeightHealth = 50.0f;
    [Export] public float WeightDamageTaken = 2.0f;

    [ExportGroup("Flow Thresholds")]
    [Export] public float  AnxietyThreshold = -0.2f;
    [Export] public float BoredomThreshold = 0.2f;

    [ExportGroup("Director Economy")]
    [Export] public float PointsPerSecond = 2.0f;
    [Export] public float BenefitActionPoints { get; private set; }
    [Export] public float NegativeActionPoints { get; private set; }

    private Timer _checkTimer;
    private List<DirectorAction> _actions = new List<DirectorAction>();
    private float _currentPerformance = 0.0f;

    public override void _Ready()
    {
        if (Data == null)
        {
            Data = GetNodeOrNull<DirectorData>("DirectorData");
            if (Data == null)
            {
                Data = GetChildOrNull<DirectorData>(0);
            }
        }

        if (Data != null)
        {
            DataManager.Instance.RegisterDirectorData(Data);
        }
        else
        {
            GD.PrintErr("[Director] DirectorData not found!");
        }

        if (StateMachine == null)
        {
            StateMachine = GetNodeOrNull<DifficultyStateMachine>("DifficultyStateMachine");
            if (StateMachine == null)
            {
                StateMachine = GetChildOrNull<DifficultyStateMachine>(0);
            }
        }

        // Find Actions container and actions
        var actionsNode = GetNodeOrNull("Actions");
        if (actionsNode != null)
        {
            foreach (var child in actionsNode.GetChildren())
            {
                if (child is DirectorAction action)
                {
                    _actions.Add(action);
                }
            }
        }
        else
        {
            GD.PrintErr("[Director] 'Actions' node not found!");
        }

        if (StateMachine != null)
        {
            StateMachine.Initialize(this, _actions);
        }
        else
        {
            GD.PrintErr("[Director] StateMachine not found!");
        }

        _checkTimer = new Timer();
        _checkTimer.WaitTime = CheckInterval;
        _checkTimer.OneShot = false;
        _checkTimer.Timeout += OnCheckTimerTimeout;
        AddChild(_checkTimer);
        _checkTimer.Start();
    }

    public override void _Process(double delta)
    {
        float dt = (float)delta;
        
        float benefitMultiplier = Mathf.Max(0, 2.0f - _currentPerformance);
        float negativeMultiplier = Mathf.Max(0, _currentPerformance);

        AddBenefitPoints(PointsPerSecond * benefitMultiplier * dt);
        AddNegativePoints(PointsPerSecond * negativeMultiplier * dt);
    }

    public void AddBenefitPoints(float amount)
    {
        BenefitActionPoints += amount;
    }

    public void SpendBenefitPoints(float amount)
    {
        BenefitActionPoints = Mathf.Max(0, BenefitActionPoints - amount);
    }

    public void AddNegativePoints(float amount)
    {
        NegativeActionPoints += amount;
    }

    public void SpendNegativePoints(float amount)
    {
        NegativeActionPoints = Mathf.Max(0, NegativeActionPoints - amount);
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
        _currentPerformance = CalculateCurrentPerformance();

        SignalManager.Instance.EmitSignal(nameof(SignalManager.PerformanceMetricsUpdated), expectedPerformance, _currentPerformance);

        UpdateGameState(_currentPerformance);
    }

    private float CalculateExpectedPerformance(float time)
    {
        // Ep[t]
        if (ExpectedPerformanceCurve != null)
        {
            float normalizedTime = Mathf.Clamp(time / ExpectedSessionDuration, 0f, 1f);
            return ExpectedPerformanceCurve.Sample(normalizedTime) * 100f; 
        }
        
        // Fallback linear formula
        return 10.0f * (time / 60.0f) + 10.0f; 
    }

    private float CalculateCurrentPerformance()
    {
        // Flow Calculation based on Recent Activity (6s window)
        // 1. Recent Net Enemy Pressure = (RecentSpawned - RecentKilled)
        // 2. Recent Damage Pressure = RecentDamageTaken
        
        float recentSpawned = Data.Performance.RecentSpawnedValue;
        float recentKilled = Data.Performance.RecentKilledValue;
        float recentDamage = Data.Performance.RecentDamageTaken;

        // Net Pressure: Positive means more enemies spawned than killed (Overwhelmed)
        // Negative means more killed than spawned (Clearing fast)
        float netEnemyPressure = recentSpawned - recentKilled;
        
        // Normalize Pressure?
        // We want Flow between -1 (Anxiety) and 1 (Boredom).
        // High Pressure -> Anxiety (-1).
        // Low/Negative Pressure -> Boredom (1).
        
        // Sensitivity factors
        float enemySensitivity = 0.2f; // 5 net enemies = 1.0 pressure
        float damageSensitivity = 0.05f; // 20 damage = 1.0 pressure

        float totalPressure = (netEnemyPressure * enemySensitivity) + (recentDamage * damageSensitivity);

        // Flow = 1.0 - TotalPressure
        // If Pressure = 0 -> Flow = 1.0 (Boredom)
        // If Pressure = 2.0 -> Flow = -1.0 (Anxiety)
        
        float flow = 1.0f - totalPressure;
        
        return flow;
    }

    private void UpdateGameState(float ratio)
    {
        if (StateMachine == null) return;

        if (ratio < AnxietyThreshold)
        {
            // Player is struggling -> Reduce Difficulty
            StateMachine.TransitionTo(DifficultyStateMachine.State.Reduce);
        }
        else if (ratio > BoredomThreshold)
        {
            // Player is doing too well -> Intensify Difficulty
            StateMachine.TransitionTo(DifficultyStateMachine.State.Intensify);
        }
        else
        {
            // Flow Channel -> Maintain
            StateMachine.TransitionTo(DifficultyStateMachine.State.Maintain);
        }
    }
}
