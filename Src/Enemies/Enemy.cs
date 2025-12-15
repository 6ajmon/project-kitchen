using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Export] public HitboxComponent HitboxComponent;
    [Export] public HealthComponent HealthComponent;
    [Export] public float DifficultyValue = 1.0f;

    [Export] public float Speed = 100f;
    [Export] public float Damage = 10f;

    public override void _Ready()
    {
        EnemyManager.Instance.RegisterEnemy(this);
        DataManager.Instance.RegisterEnemySpawn(DifficultyValue);

        if (HitboxComponent != null)
        {
            HitboxComponent.AreaEntered += HitboxAreaEntered;
        }
        if (HealthComponent != null)
        {
            HealthComponent.Died += OnDied;
        }
    }
    private void HitboxAreaEntered(Area2D area)
    {
        if (area.IsInGroup("PlayerBullet"))
        {
            var playerBullet = area as PlayerBullet;  
            if (playerBullet != null && HitboxComponent != null)  
            {
                HitboxComponent.Damage(playerBullet.GetDamage());  
                playerBullet.HandleContact();
                SignalManager.Instance.EmitSignal(nameof(SignalManager.WeaponHit));
            }
        }
    }
    private void OnDied()
    {
        EnemyManager.Instance.UnregisterEnemy(this);
        DataManager.Instance.RegisterEnemyDeath(DifficultyValue);
        SignalManager.Instance.EmitSignal(nameof(SignalManager.EnemyKilled));
        QueueFree();
    }

    public virtual void ApplyBuff(float percentage)
    {
        // Percentage is like 0.05 for 5%
        float multiplier = 1.0f + percentage;

        Speed *= multiplier;
        Damage *= multiplier;
        Scale *= multiplier;
        
        if (HealthComponent != null)
        {
            float oldMax = HealthComponent.MaxHealth;
            float newMax = oldMax * multiplier;
            HealthComponent.MaxHealth = newMax;
            // Heal the difference so current health percentage stays same or just add the flat amount?
            // Usually in games, if you buff max HP, you also increase current HP by the same amount or ratio.
            // Let's increase current health by the difference.
            HealthComponent.Heal(newMax - oldMax);
        }
        
        // Visual feedback could be added here (scale up, change color)
        Modulate = Modulate.Lerp(Colors.Red, 0.2f);
    }
}
