using Godot;
using System;
using System.Collections.Generic;

public partial class PerformanceData : Node
{
    public float AverageAccuracy => ShotsFired > 0 ? (float)ShotsHit / ShotsFired : 0f;
    public float TotalDamageTaken { get; set; }
    public double TotalSessionTime { get; set; }
    public double TimeSinceLastHit { get; set; }
    public int TotalKills { get; set; }

    public int ShotsFired { get; set; }
    public int ShotsHit { get; set; }

    // Recent Buffers (6 seconds window)
    private TimeBuffer _recentDamageBuffer = new TimeBuffer(6.0f);
    private TimeBuffer _recentSpawnBuffer = new TimeBuffer(6.0f);
    private TimeBuffer _recentKillBuffer = new TimeBuffer(6.0f);

    public float RecentDamageTaken => _recentDamageBuffer.GetSum(TotalSessionTime);
    public float RecentSpawnedValue => _recentSpawnBuffer.GetSum(TotalSessionTime);
    public float RecentKilledValue => _recentKillBuffer.GetSum(TotalSessionTime);

    public void RegisterDamageTaken(float amount)
    {
        TotalDamageTaken += amount;
        _recentDamageBuffer.Add(TotalSessionTime, amount);
    }

    public void RegisterEnemySpawn(float value)
    {
        _recentSpawnBuffer.Add(TotalSessionTime, value);
    }

    public void RegisterEnemyKill(float value)
    {
        TotalKills++;
        _recentKillBuffer.Add(TotalSessionTime, value);
    }

    public void RegisterShotFired() => ShotsFired++;
    public void RegisterShotHit() => ShotsHit++;
    public void RegisterKill() => TotalKills++; // Legacy, kept for compatibility if needed

    private class TimeBuffer
    {
        private struct Entry { public double Time; public float Value; }
        private Queue<Entry> _buffer = new Queue<Entry>();
        private float _sum = 0;
        private double _window;

        public TimeBuffer(double window) { _window = window; }

        public void Add(double time, float value) {
            _buffer.Enqueue(new Entry { Time = time, Value = value });
            _sum += value;
        }

        public float GetSum(double currentTime) {
            while (_buffer.Count > 0 && currentTime - _buffer.Peek().Time > _window) {
                _sum -= _buffer.Dequeue().Value;
            }
            return _sum;
        }
    }
}