using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class RoomSeparator : Node2D
{
    [Export] public int TileSize = 16;
    [Export] public float SimulationTimeout = 10.0f; // Maximum time to run simulation

    private List<RigidBody2D> _physicsBodies = new List<RigidBody2D>();
    private bool _simulationRunning = false;
    private float _simulationTimer = 0.0f;
    private int _bodiesAsleep = 0;
    private TaskCompletionSource<bool> _simulationComplete;
    
    public List<Rect2I> SeparateCells(List<Rect2I> cells, int iterations)
    {
        // Start the physics-based separation
        var task = SeparateCellsWithPhysics(cells);
        
        // Wait for the task to complete (blocking call)
        task.Wait();
        
        // Return the result
        return task.Result;
    }
    
    public async Task<List<Rect2I>> SeparateCellsWithPhysics(List<Rect2I> cells)
    {
        // Create physics bodies for each cell
        CreatePhysicsBodies(cells);
        
        // Start the simulation
        _simulationComplete = new TaskCompletionSource<bool>();
        _simulationRunning = true;
        _simulationTimer = 0.0f;
        _bodiesAsleep = 0;
        
        // Set up process to monitor the simulation
        ProcessMode = ProcessModeEnum.Always;
        SetProcess(true);
        
        // Wait for simulation to complete
        await ToSignal(this, "simulation_completed");
        
        // Get the final positions
        List<Rect2I> result = GetFinalPositions();
        
        // Clean up physics bodies
        CleanupPhysicsBodies();
        
        return result;
    }
    
    private void CreatePhysicsBodies(List<Rect2I> cells)
    {
        foreach (var cell in cells)
        {
            // Create rigid body for the cell
            RigidBody2D body = new RigidBody2D();
            body.Position = new Vector2(cell.Position.X + cell.Size.X / 2, cell.Position.Y + cell.Size.Y / 2);
            body.CanSleep = true;
            body.LinearDamp = 1.0f;
            body.AngularDamp = 5.0f;
            body.GravityScale = 0; // Disable gravity effect
            body.LockRotation = true; // Prevent rotation
            body.Mass = cell.Size.X * cell.Size.Y / 1000.0f; // Mass proportional to area
            
            // Create collision shape matching cell size
            CollisionShape2D shape = new CollisionShape2D();
            RectangleShape2D rectShape = new RectangleShape2D();
            rectShape.Size = new Vector2(cell.Size.X, cell.Size.Y);
            shape.Shape = rectShape;
            
            // Add collision shape to body
            body.AddChild(shape);
            
            // Add body to scene and to our list
            AddChild(body);
            _physicsBodies.Add(body);
            
            // Store original rect dimensions for later reference
            body.SetMeta("original_size", new Vector2(cell.Size.X, cell.Size.Y));
        }
    }
    
    private List<Rect2I> GetFinalPositions()
    {
        List<Rect2I> result = new List<Rect2I>();
        
        foreach (var body in _physicsBodies)
        {
            // Get the body position (center of the rectangle)
            Vector2 position = body.Position;
            Vector2 originalSize = (Vector2)body.GetMeta("original_size");
            
            // Calculate the top-left corner
            Vector2 topLeft = position - originalSize / 2;
            
            // Ensure grid alignment
            int gridX = Mathf.RoundToInt(topLeft.X / TileSize) * TileSize;
            int gridY = Mathf.RoundToInt(topLeft.Y / TileSize) * TileSize;
            
            // Create a grid-aligned rectangle
            Rect2I rect = new Rect2I(
                gridX,
                gridY,
                (int)originalSize.X,
                (int)originalSize.Y
            );
            
            result.Add(rect);
        }
        
        return result;
    }
    
    private void CleanupPhysicsBodies()
    {
        foreach (var body in _physicsBodies)
        {
            body.QueueFree();
        }
        
        _physicsBodies.Clear();
        _simulationRunning = false;
        SetProcess(false);
    }
    
    public override void _Process(double delta)
    {
        if (!_simulationRunning)
            return;
            
        _simulationTimer += (float)delta;
        
        // Check if all bodies are asleep or we've exceeded the timeout
        if (_simulationTimer > SimulationTimeout)
        {
            EmitSignal("simulation_completed");
            _simulationRunning = false;
            return;
        }
        
        // Count sleeping bodies
        _bodiesAsleep = 0;
        foreach (var body in _physicsBodies)
        {
            if (body.Sleeping || body.LinearVelocity.LengthSquared() < 0.1f)
            {
                _bodiesAsleep++;
            }
        }
        
        // If all bodies are asleep, the simulation is complete
        if (_bodiesAsleep == _physicsBodies.Count)
        {
            EmitSignal("simulation_completed");
            _simulationRunning = false;
        }
    }
    
    // Keep the old separation method for fallback or comparison
    public List<Rect2I> SeparateCellsStep(List<Rect2I> cells)
    {
        List<Rect2I> result = new List<Rect2I>(cells);
        
        for (int i = 0; i < result.Count; i++)
        {
            Vector2I moveVector = Vector2I.Zero;
            Rect2I cellA = result[i];
            
            for (int j = 0; j < result.Count; j++)
            {
                if (i == j) continue;
                
                Rect2I cellB = result[j];
                
                if (cellA.Intersects(cellB))
                {
                    // Calculate overlap and push direction
                    int overlapX = Math.Min(
                        cellA.Position.X + cellA.Size.X - cellB.Position.X,
                        cellB.Position.X + cellB.Size.X - cellA.Position.X
                    );
                    
                    int overlapY = Math.Min(
                        cellA.Position.Y + cellA.Size.Y - cellB.Position.Y,
                        cellB.Position.Y + cellB.Size.Y - cellA.Position.Y
                    );
                    
                    // Determine which axis has the smallest overlap
                    if (overlapX < overlapY)
                    {
                        int dir = (cellA.Position.X + cellA.Size.X / 2) < (cellB.Position.X + cellB.Size.X / 2) ? -1 : 1;
                        moveVector.X += dir * (overlapX / 2 + TileSize);
                    }
                    else
                    {
                        int dir = (cellA.Position.Y + cellA.Size.Y / 2) < (cellB.Position.Y + cellB.Size.Y / 2) ? -1 : 1;
                        moveVector.Y += dir * (overlapY / 2 + TileSize);
                    }
                }
            }
            
            // Apply movement to cell (maintain grid alignment)
            if (moveVector != Vector2I.Zero)
            {
                Vector2I newPosition = cellA.Position + moveVector;
                
                // Ensure grid alignment
                newPosition.X = (newPosition.X / TileSize) * TileSize;
                newPosition.Y = (newPosition.Y / TileSize) * TileSize;
                
                result[i] = new Rect2I(
                    newPosition,
                    cellA.Size
                );
            }
        }
        
        return result;
    }
    
    [Signal]
    public delegate void SimulationCompletedEventHandler();
}