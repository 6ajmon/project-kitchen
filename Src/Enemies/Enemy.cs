using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Export] public HitboxComponent HitboxComponent;
    [Export] public HealthComponent HealthComponent;
    [Export] public float DifficultyValue = 1.0f;

    public override void _Ready()
    {
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
        DataManager.Instance.RegisterEnemyDeath(DifficultyValue);
        SignalManager.Instance.EmitSignal(nameof(SignalManager.EnemyKilled));
        QueueFree();
    }
}
