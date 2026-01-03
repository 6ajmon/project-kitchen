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
    private TimeBuffer _recentDirectorSpawnBuffer = new TimeBuffer(6.0f);
    
    // Long Term Buffers (30 seconds window) for Flailing detection
    private TimeBuffer _longTermDamageBuffer = new TimeBuffer(30.0f);

    // Kill rate tracking (15 second window for responsive feedback)
    private TimeBuffer _killRateBuffer = new TimeBuffer(15.0f);
    
    // Damage rate tracking (15 second window for responsive feedback)
    private TimeBuffer _damageRateBuffer = new TimeBuffer(15.0f);
    
    // HP tracking for flailing detection (rapid HP drop)
    private float _previousHealth = -1f;
    private TimeBuffer _hpDropBuffer = new TimeBuffer(5.0f); // 5-second window for rapid HP drops

    public float RecentDamageTaken => _recentDamageBuffer.GetSum(TotalSessionTime);
    public float LongTermDamageTaken => _longTermDamageBuffer.GetSum(TotalSessionTime);
    public float RecentSpawnedValue => _recentSpawnBuffer.GetSum(TotalSessionTime);
    public float RecentDirectorSpawnValue => _recentDirectorSpawnBuffer.GetSum(TotalSessionTime);
    public float RecentKilledValue => _recentKillBuffer.GetSum(TotalSessionTime);
    
    /// <summary>
    /// Returns kills in the last 15 seconds of gameplay.
    /// </summary>
    public float KillRate => _killRateBuffer.GetSum(TotalSessionTime);
    
    /// <summary>
    /// Returns damage taken in the last 15 seconds of gameplay.
    /// </summary>
    public float DamageTakenRate => _damageRateBuffer.GetSum(TotalSessionTime);
    
    /// <summary>
    /// Returns the total HP dropped in the last 5 seconds (for flailing detection).
    /// </summary>
    public float RecentHPDrop => _hpDropBuffer.GetSum(TotalSessionTime);

    public void RegisterDamageTaken(float amount)
    {
        TotalDamageTaken += amount;
        _recentDamageBuffer.Add(TotalSessionTime, amount);
        _longTermDamageBuffer.Add(TotalSessionTime, amount);
        _damageRateBuffer.Add(TotalSessionTime, amount);
    }

    /// <summary>
    /// Updates HP tracking for flailing detection. Should be called every frame with current health.
    /// </summary>
    public void UpdateHealthTracking(float currentHealth)
    {
        if (_previousHealth >= 0 && currentHealth < _previousHealth)
        {
            float hpDrop = _previousHealth - currentHealth;
            _hpDropBuffer.Add(TotalSessionTime, hpDrop);
        }
        _previousHealth = currentHealth;
    }

    public void RegisterDirectorSpawn(float value)
    {
        _recentDirectorSpawnBuffer.Add(TotalSessionTime, value);
    }

    public void RegisterEnemySpawn(float value)
    {
        _recentSpawnBuffer.Add(TotalSessionTime, value);
    }

    public void RegisterEnemyKill(float value)
    {
        TotalKills++;
        _recentKillBuffer.Add(TotalSessionTime, value);
        _killRateBuffer.Add(TotalSessionTime, 1.0f); // Count kills for rate calculation
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