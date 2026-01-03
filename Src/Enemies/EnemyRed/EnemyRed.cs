using Godot;
using System;

public partial class EnemyRed : Enemy
{
    [Export] public float ChaseRange = 200f;
    [Export] public float DamageCooldown = 1.0f;

    private Player player;
    private double lastDamageTime = 0.0;

    public override void _Ready()
    {
        base._Ready();
        // Set default values if needed, or rely on Inspector
        // Speed and Damage are now in base Enemy
        
        player = GetNodeOrNull<Player>("/root/Level/Player");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= ChaseRange)
        {
            ChasePlayer();
        }
        else
        {
            Velocity = Vector2.Zero;
        }
        MoveAndSlide();

        if (player != null && HitboxComponent != null)
        {
            var currentTime = Time.GetUnixTimeFromSystem();
            if (IsCollidingWithPlayer() && currentTime - lastDamageTime >= DamageCooldown)
            {
                var playerHitbox = player.GetNodeOrNull<HitboxComponent>("HitboxComponent");
                if (playerHitbox != null)
                {
                    playerHitbox.Damage(Damage);
                    lastDamageTime = currentTime;
                }
            }
        }
    }

    private void ChasePlayer()
    {
        if (player != null)
        {
            Vector2 direction = (player.GlobalPosition - GlobalPosition).Normalized();
            Velocity = direction * Speed;
        }
    }

    private bool IsCollidingWithPlayer()
    {
        for (int i = 0; i < GetSlideCollisionCount(); i++)
        {
            var collision = GetSlideCollision(i);
            if (collision.GetCollider() == player)
            {
                return true;
            }
        }
        return false;
    }
}
