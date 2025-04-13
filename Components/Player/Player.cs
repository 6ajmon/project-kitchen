using Godot;
using System;

public partial class Player : Node2D
{
	[Export] private float speed = 300;
	private AStarGrid2D aStarGrid;
	Godot.Collections.Array<Vector2I> currentPath;
	public Vector2I playerPosition = new(5, 7);
	public bool isSelected = false;
	private Grid parent = new();
	public override void _Ready()
	{
		parent = GetParent<Grid>();
		
		GlobalPosition = parent.MapToLocal(playerPosition);
	}
	public void SetUpGrid()
	{
		aStarGrid = new();
		aStarGrid.Region = parent.GetUsedRect();
		aStarGrid.CellSize = parent.TileSet.TileSize;
		aStarGrid.DiagonalMode = AStarGrid2D.DiagonalModeEnum.OnlyIfNoObstacles;
		aStarGrid.Update();
	}
	public void SetCollisionsOnGrid(Vector2I mapPosition)
	{
		aStarGrid.SetPointSolid(mapPosition, true);
	}

	public override void _PhysicsProcess(double delta)
	{
		if (currentPath == null)
		{
			return;
		}
		var targetPosition = parent.MapToLocal(currentPath[0]);
		GlobalPosition = GlobalPosition.MoveToward(targetPosition, (float)(speed * delta));

		if (GlobalPosition == targetPosition)
		{
			currentPath.RemoveAt(0);
			if (currentPath.Count == 0)
			{
				currentPath = null;
			}
		}
	}
	public void Select()
	{
		isSelected = !isSelected;
	}
	public void Move(Vector2I newPosition){
		if (isSelected){
			var path = aStarGrid.GetIdPath(
				parent.LocalToMap(GlobalPosition), 
				newPosition
			);
			if (path != null)
			{
				currentPath = path;
			}
			Select();
		}
	}
}
