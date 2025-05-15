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
		// Get input direction (WASD or arrow keys)
		Vector2 inputDirection = Input.GetVector("MoveLeft", "MoveRight", "MoveUp", "MoveDown");
		
		// Set the velocity based on input direction and speed
		Velocity = inputDirection * speed;
		
		// Move the character and handle collisions
		MoveAndSlide();
	}
}
