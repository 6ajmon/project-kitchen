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

    [ExportGroup("Cp Calculation")]
    [Export(PropertyHint.Range, "0,1,0.05")] 
    public float CpBaseline = 0.5f;            // Neutral starting point for Cp
    [Export(PropertyHint.Range, "0,0.5,0.05")] 
    public float KillBonus = 0.3f;             // Max bonus from killing enemies
    [Export(PropertyHint.Range, "0,0.5,0.05")] 
    public float DamagePenalty = 0.3f;         // Max penalty from taking damage
    
    [ExportGroup("Health Safety")]
    [Export(PropertyHint.Range, "0.3,0.7,0.05")] 
    public float HealthSafetyThreshold = 0.5f; // HP% above this = safe (no HP penalty)
    [Export(PropertyHint.Range, "1,3,0.1")] 
    public float LowHealthMultiplier = 2.0f;   // How much low HP amplifies damage penalty

    [ExportGroup("Expected Values (Normalization - 15s windows)")]
    [Export] public float ExpectedKillsPer15s = 5.0f;       // Expected kills in 15 seconds
    [Export] public float MaxDamagePer15s = 20.0f;          // Max expected damage in 15 seconds

    [ExportGroup("Flow Thresholds (Ratio-based)")]
    [Export] public float ReduceThreshold = 0.8f;      // Ratio < 0.8 -> Player struggling
    [Export] public float IntensifyThreshold = 1.2f;   // Ratio > 1.2 -> Player dominating

    [ExportGroup("Flailing Detection")]
    [Export] public float FlailingHPDropThreshold = 30.0f;  // HP drop in 5 seconds to trigger flailing
    [Export] public float FlailingHealthThreshold = 0.3f;   // Health% below which flailing is checked

    [ExportGroup("Director Economy")]
    [Export] public float BasePointsPerSecond = 1.0f;  // Base points at threshold edge
    [Export] public float MaxPointsMultiplier = 4.0f;  // Max multiplier at extreme ratios
    [Export] public float BenefitActionPoints { get; private set; }
    [Export] public float NegativeActionPoints { get; private set; }

    [ExportGroup("Smoothing")]
    [Export] public float RatioSmoothingFactor = 0.1f;

    private Timer _checkTimer;
    private List<DirectorAction> _actions = new List<DirectorAction>();
    private float _smoothedRatio = 1.0f; // Start at perfect balance
    
    /// <summary>
    /// Current smoothed performance ratio (Cp/Ep). Publicly accessible for states.
    /// </summary>
    public float CurrentRatio => _smoothedRatio;
    
    /// <summary>
    /// Calculates points multiplier based on how far ratio is from Flow zone.
    /// At threshold edge (0.8 or 1.2): returns 1.0
    /// At extreme (0.0 or 2.0+): returns MaxPointsMultiplier
    /// </summary>
    public float GetPointsMultiplier()
    {
        float distanceFromFlow;
        
        if (_smoothedRatio < ReduceThreshold)
        {
            // REDUCE zone: distance from 0.8 toward 0
            // At 0.8 -> 0, at 0.0 -> 0.8
            distanceFromFlow = ReduceThreshold - _smoothedRatio;
            float maxDistance = ReduceThreshold; // 0.8
            float normalizedDistance = Mathf.Clamp(distanceFromFlow / maxDistance, 0f, 1f);
            return 1.0f + (normalizedDistance * (MaxPointsMultiplier - 1.0f));
        }
        else if (_smoothedRatio > IntensifyThreshold)
        {
            // INTENSIFY zone: distance from 1.2 toward infinity (capped at 2.0)
            // At 1.2 -> 0, at 2.0 -> 0.8
            distanceFromFlow = _smoothedRatio - IntensifyThreshold;
            float maxDistance = IntensifyThreshold - ReduceThreshold; // 0.8 (same scale as reduce)
            float normalizedDistance = Mathf.Clamp(distanceFromFlow / maxDistance, 0f, 1f);
            return 1.0f + (normalizedDistance * (MaxPointsMultiplier - 1.0f));
        }
        
        // In FLOW zone - no points generation
        return 0f;
    }

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
        // Delegate behavior to current state
        StateMachine?.UpdateState(delta);
    }

    public void TrySpendBenefitPoints()
    {
        if (Data?.Performance == null) return;
        TryExecuteWeightedAction(ActionType.Benefit, BenefitActionPoints, Data.Performance.TotalSessionTime, (cost) => SpendBenefitPoints(cost));
    }

    public void TrySpendNegativePoints()
    {
        if (Data?.Performance == null) return;
        TryExecuteWeightedAction(ActionType.Negative, NegativeActionPoints, Data.Performance.TotalSessionTime, (cost) => SpendNegativePoints(cost));
    }

    private void TryExecuteWeightedAction(ActionType type, float availablePoints, double currentTime, Action<float> spendCallback)
    {
        // Filter candidates
        var candidates = _actions.Where(a => a.ActionType == type && a.CanExecute(availablePoints, currentTime)).ToList();

        if (candidates.Count == 0) return;

        // Calculate Total Weight
        float totalWeight = candidates.Sum(a => a.Weight);

        // Weighted Random Selection
        float randomValue = GD.Randf() * totalWeight;
        float currentSum = 0;

        foreach (var action in candidates)
        {
            currentSum += action.Weight;
            if (randomValue <= currentSum)
            {
                // Execute this action
                action.Execute(currentTime);
                spendCallback(action.Cost);
                break;
            }
        }
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
        
        // Update HP tracking for flailing detection
        Data.Performance.UpdateHealthTracking(Data.Player.CurrentHealth);
        
        // Step 1: Calculate Cp (Current Player Power)
        float cp = CalculateCurrentPlayerPower();
        
        // Step 2: Get Ep (Expected Performance) from curve
        float ep = CalculateExpectedPerformance(time);
        
        // Step 3: Calculate Ratio = Cp / Ep
        float rawRatio = ep > 0 ? cp / ep : 1.0f;
        
        // Smooth the ratio to prevent erratic behavior
        _smoothedRatio = Mathf.Lerp(_smoothedRatio, rawRatio, RatioSmoothingFactor);
        
        // Step 4: Check for Flailing (emergency override)
        bool isFlailing = DetectFlailing();

        // Emit performance metrics for debugging/UI
        SignalManager.Instance.EmitSignal(nameof(SignalManager.PerformanceMetricsUpdated), 
            ep,                              // Expected performance
            cp,                              // Current player power
            _smoothedRatio,                  // Smoothed ratio
            BenefitActionPoints, 
            NegativeActionPoints,
            Data.Performance.KillRate,       // Kills per minute
            Data.Performance.DamageTakenRate,// Damage per minute
            isFlailing ? 1.0f : 0.0f         // Flailing indicator
        );

        // Step 5: Determine state based on ratio and flailing
        UpdateGameState(_smoothedRatio, isFlailing);
    }

    /// <summary>
    /// Calculates Cp (Current Player Power) using performance-based formula:
    /// Cp = Baseline + KillBonus - (DamagePenalty * HealthMultiplier)
    /// 
    /// Health only affects Cp when below safety threshold (e.g. 50%):
    /// - Above threshold: HP doesn't matter, focus on kill/damage rate
    /// - Below threshold: Damage penalty is amplified
    /// 
    /// Result is clamped to [0, 1] range
    /// </summary>
    private float CalculateCurrentPlayerPower()
    {
        // Health ratio (0 to 1)
        float healthRatio = Data.Player.MaxHealth > 0 
            ? Data.Player.CurrentHealth / Data.Player.MaxHealth 
            : 1.0f;
        
        // Normalized Kill Rate (0 to 1+, based on expected kills in 15 seconds)
        float killRate = Data.Performance.KillRate;
        float normalizedKillRate = Mathf.Clamp(killRate / ExpectedKillsPer15s, 0f, 1.5f);
        
        // Normalized Damage Taken Rate (0 to 1, higher is worse)
        float damageTakenRate = Data.Performance.DamageTakenRate;
        float normalizedDamageRate = Mathf.Clamp(damageTakenRate / MaxDamagePer15s, 0f, 1.0f);
        
        // Health Multiplier - only activates when HP is below safety threshold
        // Above threshold: multiplier = 1.0 (damage penalty normal)
        // Below threshold: multiplier scales up to LowHealthMultiplier
        float healthMultiplier = 1.0f;
        if (healthRatio < HealthSafetyThreshold)
        {
            // How far below the threshold (0 = at threshold, 1 = at 0 HP)
            float dangerLevel = 1.0f - (healthRatio / HealthSafetyThreshold);
            healthMultiplier = 1.0f + (dangerLevel * (LowHealthMultiplier - 1.0f));
        }
        
        // Final Cp calculation:
        // Start at baseline, add kills bonus, subtract damage penalty (amplified by low HP)
        float cp = CpBaseline 
                 + (KillBonus * normalizedKillRate) 
                 - (DamagePenalty * normalizedDamageRate * healthMultiplier);
        
        // Clamp to reasonable range [0, 1]
        return Mathf.Clamp(cp, 0f, 1.0f);
    }

    private float CalculateExpectedPerformance(float time)
    {
        // Ep[t] - Expected performance at time t
        // Uses GameManager's shared difficulty curve
        float difficulty = GameManager.Instance?.CurrentDifficulty ?? 0f;
        
        // Map difficulty (0-1) to expected Cp range
        // At difficulty 0: Ep = CpBaseline (player just started, no expectations)
        // At difficulty 1: Ep = CpBaseline + 0.15 (player should be performing well)
        return CpBaseline + (0.15f * difficulty);
    }

    /// <summary>
    /// Detects "Flailing" - when player's HP is dropping critically fast.
    /// This is an emergency override that forces REDUCE state.
    /// </summary>
    private bool DetectFlailing()
    {
        if (Data.Player.MaxHealth <= 0) return false;
        
        float healthPercent = Data.Player.CurrentHealth / Data.Player.MaxHealth;
        float recentHPDrop = Data.Performance.RecentHPDrop;
        
        // Flailing = Low health AND rapid HP drop in recent seconds
        return healthPercent < FlailingHealthThreshold && recentHPDrop >= FlailingHPDropThreshold;
    }

    private void UpdateGameState(float ratio, bool isFlailing)
    {
        if (StateMachine == null) return;

        // Flailing has priority - immediately rescue the player
        if (isFlailing)
        {
            StateMachine.TransitionTo(DifficultyStateMachine.State.Reduce);
            return;
        }

        // Standard ratio-based state determination
        if (ratio < ReduceThreshold)
        {
            // Ratio < 0.8: Player is struggling, reduce difficulty
            StateMachine.TransitionTo(DifficultyStateMachine.State.Reduce);
        }
        else if (ratio > IntensifyThreshold)
        {
            // Ratio > 1.2: Player is dominating, intensify difficulty
            StateMachine.TransitionTo(DifficultyStateMachine.State.Intensify);
        }
        else
        {
            // 0.8 <= Ratio <= 1.2: Player is in Flow, maintain
            StateMachine.TransitionTo(DifficultyStateMachine.State.Maintain);
        }
    }
}
