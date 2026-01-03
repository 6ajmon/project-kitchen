using Godot;
using System;

public partial class EnemyRed : Enemy
{
    [Export] public float ChaseRange = 200f;

    private Player player;

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
            ChasePlayer(delta);
        }

        if (player != null && HitboxComponent != null)
        {
            if (OverlapsBody(player))
            {
                var playerHitbox = player.GetNodeOrNull<HitboxComponent>("HitboxComponent");
                if (playerHitbox != null)
                {
                    playerHitbox.Damage(Damage);
                }
            }
        }
    }

    private void ChasePlayer(double delta)
    {
        if (player != null)
        {
            Vector2 direction = (player.GlobalPosition - GlobalPosition).Normalized();
            Position += direction * Speed * (float)delta;
        }
    }
}
