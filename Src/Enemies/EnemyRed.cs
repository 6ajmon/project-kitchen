using Godot;
using System;

public partial class EnemyRed : Enemy
{
    [Export] public float Speed = 200f;
    [Export] public float damage = 10f;
    [Export] public float chaseRange = 300f;

    private Player player;
    public override void _Ready()
    {
        base._Ready();

        player = GetNode<Player>("/root/Level/Player");
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
    }

    private void ChasePlayer()
    {
        // Logic to chase the player
        if (player != null)
        {
            Vector2 direction = (player.GlobalPosition - GlobalPosition).Normalized();
            Velocity = direction * Speed;
        }
    }
}
