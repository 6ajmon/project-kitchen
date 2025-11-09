using Godot;
using System;

public partial class Player : CharacterBody2D
{
	[Export] private float speed = 200;
	
	public override void _Ready()
	{
		
	}
	
	public override void _PhysicsProcess(double delta)
	{
		Vector2 inputDirection = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");
		
		Velocity = inputDirection * speed;
		
		MoveAndSlide();
	}
}
