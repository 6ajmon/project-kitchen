using Godot;
using System;

public partial class PlayerBullet : Area2D
{
    private float damage = 10f;
    [Export] private float speed = 400;
    public Vector2 Direction { get; set; }
    public float GetDamage()
    {
        return damage;
    }


    public override void _PhysicsProcess(double delta)
    {
        Position += Direction * speed * (float)delta;
    }
}
