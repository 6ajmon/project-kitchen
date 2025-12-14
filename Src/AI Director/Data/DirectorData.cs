using Godot;
using System;

public partial class DirectorData : Node
{
    [Export] public PerformanceData Performance { get; private set; }
    [Export] public PlayerData Player { get; private set; }

    public override void _Ready()
    {
        if (Performance == null) Performance = GetNodeOrNull<PerformanceData>("PerformanceData");
        if (Player == null) Player = GetNodeOrNull<PlayerData>("PlayerData");
    }
}
