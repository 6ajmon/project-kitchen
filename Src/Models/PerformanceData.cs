using Godot;
using System;

public partial class PerformanceData : Node
{
    public float AverageAccuracy { get; private set; }
    public float TotalDamageTaken { get; private set; }
    public double TotalSessionTime { get; private set; }
    public double TimeSinceLastHit { get; private set; }
    public int TotalKills { get; private set; }
}