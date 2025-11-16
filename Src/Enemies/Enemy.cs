using Godot;
using System;

public partial class Enemy : CharacterBody2D
{
    [Export] public HitboxComponent HitboxComponent;
    [Export] public HealthComponent HealthComponent;
    public override void _Ready()
    {
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
            }
        }
    }
    private void OnDied()
    {
        QueueFree();
    }
}
