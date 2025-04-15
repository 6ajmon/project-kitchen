using Godot;
using System;
using System.Collections.Generic;

public partial class RoomSeparator : Node2D
{
    public List<Rect2> SeparateCells(List<Rect2> cells, int iterations)
    {
        List<Rect2> result = new List<Rect2>(cells);
        
        for (int iter = 0; iter < iterations; iter++)
        {
            result = SeparateCellsStep(result);
        }
        
        return result;
    }
    
    public List<Rect2> SeparateCellsStep(List<Rect2> cells)
    {
        List<Rect2> result = new List<Rect2>(cells);
        
        for (int i = 0; i < result.Count; i++)
        {
            Vector2 moveVector = Vector2.Zero;
            Rect2 cellA = result[i];
            
            for (int j = 0; j < result.Count; j++)
            {
                if (i == j) continue;
                
                Rect2 cellB = result[j];
                
                if (cellA.Intersects(cellB))
                {
                    // Calculate overlap and push direction
                    float overlapX = Math.Min(
                        cellA.Position.X + cellA.Size.X - cellB.Position.X,
                        cellB.Position.X + cellB.Size.X - cellA.Position.X
                    );
                    
                    float overlapY = Math.Min(
                        cellA.Position.Y + cellA.Size.Y - cellB.Position.Y,
                        cellB.Position.Y + cellB.Size.Y - cellA.Position.Y
                    );
                    
                    // Determine which axis has the smallest overlap
                    if (overlapX < overlapY)
                    {
                        float dir = cellA.GetCenter().X < cellB.GetCenter().X ? -1 : 1;
                        moveVector.X += dir * (overlapX / 2 + 1);
                    }
                    else
                    {
                        float dir = cellA.GetCenter().Y < cellB.GetCenter().Y ? -1 : 1;
                        moveVector.Y += dir * (overlapY / 2 + 1);
                    }
                }
            }
            
            // Apply movement to cell
            if (moveVector != Vector2.Zero)
            {
                result[i] = new Rect2(
                    cellA.Position + moveVector,
                    cellA.Size
                );
            }
        }
        
        return result;
    }
}