using Godot;
using System;

public partial class Player : Node2D
{
	[Export] private float speed = 300;
	private AStarGrid2D aStarGrid;
	Godot.Collections.Array<Vector2I> currentPath;
	public Vector2I playerPosition = new(5, 7);
	public bool isSelected = false;
	public override void _Ready()
	{
		
	}
}
