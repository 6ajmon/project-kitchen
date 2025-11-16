using Godot;
using System;

public partial class Player : CharacterBody2D
{
	private PackedScene playerBulletScene;
	[Export] private float speed = 200;
	
	public override void _Ready()
	{
		playerBulletScene = GD.Load<PackedScene>("res://Src/Projectiles/PlayerBullet/PlayerBullet.tscn");
	}
	
	public override void _PhysicsProcess(double delta)
	{
		Vector2 inputDirection = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");
		
		Velocity = inputDirection * speed;
		
		if (Input.IsActionJustPressed("PlayerShoot"))
		{
			Shoot();
		}

		MoveAndSlide();
	}

	private void Shoot()
	{
		Vector2 mousePosition = GetGlobalMousePosition();
		if (playerBulletScene != null)
		{
			PlayerBullet bullet = playerBulletScene.Instantiate<PlayerBullet>();
			bullet.GlobalPosition = GlobalPosition;
			
			Vector2 direction = (mousePosition - GlobalPosition).Normalized();
			bullet.Direction = direction;
			
			GetParent().AddChild(bullet);
		}
	}
}
