using Godot;
using System;

public partial class PlayerBullet : Area2D
{
    [Export] private float damage = 10f;
    [Export] private float pierce = 1f;
    [Export] private float speed = 400;
    [Export] private float lifespan = 5f;
    public Vector2 Direction { get; set; }
    
    public override void _Ready()
    {
        GetTree().CreateTimer(lifespan).Timeout += () => QueueFree();
    }

    public override void _PhysicsProcess(double delta)
    {
        Position += Direction * speed * (float)delta;
    }
    public float GetDamage()
    {
        return damage;
    }
    public void HandleContact()
    {
        pierce -= 1;
        if (pierce <= 0)
        {
            QueueFree();
        }
    }
}
