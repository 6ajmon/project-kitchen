using Godot;
using System;

public partial class EnemyRed : Enemy
{
    [Export] public float Speed = 200f;
    [Export] public float damage = 10f;
    [Export] public float chaseRange = 200f;
    [Export] public float damageCooldown = 1.0f;

    private Player player;
    private HitboxComponent hitboxComponent;
    private double lastDamageTime = 0.0;

    public override void _Ready()
    {
        base._Ready();

        player = GetNode<Player>("/root/Level/Player");
        hitboxComponent = GetNode<HitboxComponent>("HitboxComponent");
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (player != null && GlobalPosition.DistanceTo(player.GlobalPosition) <= chaseRange)
        {
            ChasePlayer();
        }
        else
        {
            Velocity = Vector2.Zero;
        }
        MoveAndSlide();

        if (player != null && hitboxComponent != null)
        {
            var currentTime = Time.GetUnixTimeFromSystem();
            if (IsCollidingWithPlayer() && currentTime - lastDamageTime >= damageCooldown)
            {
                var playerHitbox = player.GetNode<HitboxComponent>("HitboxComponent");
                if (playerHitbox != null)
                {
                    playerHitbox.Damage((int)damage);
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
