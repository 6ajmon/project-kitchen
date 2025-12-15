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

    private void OnPerformanceMetricsUpdated(float expected, float current, float flowRatio, float benefitPoints, float negativePoints)
    {
        // Clear existing stats
        foreach (Node child in _statTable.GetChildren())
        {
            child.QueueFree();
        }

        AddStatRow("Expected Perf", expected.ToString("F2"));
        AddStatRow("Current Perf", current.ToString("F2"));
        AddStatRow("Flow Ratio", flowRatio.ToString("F2"));
        AddStatRow("Benefit Pts", benefitPoints.ToString("F0"));
        AddStatRow("Negative Pts", negativePoints.ToString("F0"));
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
