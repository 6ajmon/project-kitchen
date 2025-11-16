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
    void HitboxAreaEntered(Area2D area)
    {
        if (area.IsInGroup("PlayerBullet"))
        {
            var playerBullet = (PlayerBullet)area;
            if (HitboxComponent != null)
            {
                HitboxComponent.Damage(playerBullet.GetDamage());
            }
        }
    }
    void OnDied()
    {
        QueueFree();
    }
}
