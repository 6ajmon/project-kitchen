using Godot;
using System;

public partial class DebugUi : CanvasLayer
{
    [Export] private Label _stateNameLabel;
    [Export] private GridContainer _statTable;
    [Export] private VBoxContainer _logContainer;

    public override void _Ready()
    {
        if (_stateNameLabel == null) _stateNameLabel = GetNode<Label>("MarginContainer/HSplitContainer/VBoxContainer/HBoxContainer/StateNameLabel");
        if (_statTable == null) _statTable = GetNode<GridContainer>("MarginContainer/HSplitContainer/VBoxContainer/StatTable");
        if (_logContainer == null) _logContainer = GetNode<VBoxContainer>("MarginContainer/HSplitContainer/PanelContainer/ScrollContainer/VBoxContainer");

        SignalManager.Instance.DifficultyStateChanged += OnDifficultyStateChanged;
        SignalManager.Instance.DirectorActionExecuted += OnDirectorActionExecuted;
        SignalManager.Instance.PerformanceMetricsUpdated += OnPerformanceMetricsUpdated;
    }

    private void OnDifficultyStateChanged(string newStateName)
    {
        _stateNameLabel.Text = $"STATE: {newStateName}";
    }

    private void OnDirectorActionExecuted(string actionName, float cost)
    {
        var label = new Label();
        label.Text = $"[{DateTime.Now:HH:mm:ss}] Action: {actionName} (Cost: {cost})";
        _logContainer.AddChild(label);
        _logContainer.MoveChild(label, 0); // Add to top

        if (_logContainer.GetChildCount() > 20)
        {
            _logContainer.GetChild(_logContainer.GetChildCount() - 1).QueueFree();
        }
    }

    private void OnPerformanceMetricsUpdated(float expectedPerformance, float currentPlayerPower, float ratio, float benefitPoints, float negativePoints, float killRate, float damageRate, float isFlailing)
    {
        // Clear existing stats
        foreach (Node child in _statTable.GetChildren())
        {
            child.QueueFree();
        }

        // Core Cp/Ep/Ratio metrics
        AddStatRow("Ep (Expected)", expectedPerformance.ToString("F3"));
        AddStatRow("Cp (Player Power)", currentPlayerPower.ToString("F3"));
        
        // Ratio with state indicator
        string ratioState = ratio < 0.8f ? "[REDUCE]" : (ratio > 1.2f ? "[INTENSIFY]" : "[FLOW]");
        AddStatRow("Ratio (Cp/Ep)", $"{ratio:F2} {ratioState}");
        
        // Flailing detection
        AddStatRow("Flailing", isFlailing > 0.5f ? "⚠ YES" : "No");
        
        // Director economy
        AddStatRow("───────────", "───────────");
        AddStatRow("Benefit Pts", benefitPoints.ToString("F0"));
        AddStatRow("Negative Pts", negativePoints.ToString("F0"));
        
        // Performance metrics
        AddStatRow("───────────", "───────────");
        AddStatRow("Kills/15s", killRate.ToString("F1"));
        AddStatRow("Damage/15s", damageRate.ToString("F1"));
        
        // Session & Difficulty info
        AddStatRow("───────────", "───────────");
        if (GameManager.Instance != null)
        {
            float sessionTime = GameManager.Instance.SessionTime;
            float sessionDuration = GameManager.Instance.SessionDuration;
            AddStatRow("Session", $"{sessionTime:F0}s / {sessionDuration:F0}s");
            AddStatRow("Difficulty", $"{GameManager.Instance.CurrentDifficulty:P0}");
        }
        if (EnemyManager.Instance != null)
        {
            AddStatRow("Spawn Rate", $"{EnemyManager.Instance.GetCurrentSpawnInterval():F1}s");
        }
    }

    private void AddStatRow(string name, string value)
    {
        var nameLabel = new Label();
        nameLabel.Text = name;
        _statTable.AddChild(nameLabel);

        var valueLabel = new Label();
        valueLabel.Text = value;
        _statTable.AddChild(valueLabel);
    }
}
