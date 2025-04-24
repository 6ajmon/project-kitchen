using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

public partial class RoomSeparator : Node2D
{
    [Export] public int TileSize = 16;
    [Export] public float SimulationTimeout = 10.0f; // Maximum time to run simulation
    [Export] public float PhysicsTimeScale = 0.1f; // Controls the speed of physics simulation (lower = slower)

    private List<RigidBody2D> _physicsBodies = new List<RigidBody2D>();
    private bool _simulationRunning = false;
    private float _simulationTimer = 0.0f;
    private int _bodiesAsleep = 0;
    private TaskCompletionSource<bool> _simulationComplete;
    private float _stableTimer = 0;
    private float _physicsStepAccumulator = 0f;

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
        await ToSignal(this, "SimulationCompleted");
        
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
            body.LinearDamp = 1.5f; // Reduced damping to show more movement
            body.AngularDamp = 8.0f; // Increase damping to settle faster
            body.GravityScale = 0; // Disable gravity effect
            body.LockRotation = true; // Prevent rotation
            body.Mass = cell.Size.X * cell.Size.Y / 1000.0f; // Mass proportional to area
            body.ContactMonitor = true; // Enable contact monitoring for better collision detection
            body.MaxContactsReported = 4; // Report up to 4 contacts
            
            // Add more initial energy to make movement more visible
            body.LinearVelocity = new Vector2(
                (float)GD.RandRange(-10, 10),
                (float)GD.RandRange(-10, 10)
            );
            
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
    
    public List<Rect2I> GetCurrentPositions()
    {
        if (_physicsBodies.Count == 0) return new List<Rect2I>();
        
        List<Rect2I> result = new List<Rect2I>();
        
        foreach (var body in _physicsBodies)
        {
            Vector2 position = body.Position;
            Vector2 originalSize = (Vector2)body.GetMeta("original_size");
            
            // Calculate top-left corner
            Vector2 topLeft = position - originalSize / 2;
            
            // Ensure grid alignment
            int gridX = Mathf.RoundToInt(topLeft.X / TileSize) * TileSize;
            int gridY = Mathf.RoundToInt(topLeft.Y / TileSize) * TileSize;
            
            result.Add(new Rect2I(
                gridX,
                gridY,
                (int)originalSize.X,
                (int)originalSize.Y
            ));
        }
        
        return result;
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
        
        // Control physics step timing with PhysicsTimeScale
        _physicsStepAccumulator += (float)delta;
        if (_physicsStepAccumulator < PhysicsTimeScale)
        {
            // Not enough time accumulated for a physics step
            return;
        }
        
        // Reset accumulator but keep any excess time
        _physicsStepAccumulator -= PhysicsTimeScale;
        
        // Apply forces to keep bodies moving and prevent early sleep
        foreach (var body in _physicsBodies)
        {
            if (!body.Sleeping && body.LinearVelocity.LengthSquared() < 5.0f)
            {
                // Apply a small random force to keep things moving
                body.ApplyCentralImpulse(new Vector2(
                    (float)GD.RandRange(-1.0, 1.0) * 0.5f,
                    (float)GD.RandRange(-1.0, 1.0) * 0.5f
                ));
            }
        }
        
        // Check if all bodies are asleep or we've exceeded the timeout
        if (_simulationTimer > SimulationTimeout)
        {
            EmitSignal("SimulationCompleted");
            _simulationRunning = false;
            return;
        }
        
        // Count sleeping bodies
        _bodiesAsleep = 0;
        bool allStable = true;
        
        foreach (var body in _physicsBodies)
        {
            if (body.Sleeping || body.LinearVelocity.LengthSquared() < 0.05f) // Lower threshold for "stable"
            {
                _bodiesAsleep++;
            }
            else
            {
                allStable = false;
            }
        }
        
        // If all bodies are stable for at least 0.5 seconds, consider simulation complete
        if (allStable)
        {
            if (_stableTimer == 0)
            {
                _stableTimer = (float)delta;
            }
            else
            {
                _stableTimer += (float)delta;
                if (_stableTimer >= 0.5f)
                {
                    EmitSignal("SimulationCompleted");
                    _simulationRunning = false;
                }
            }
        }
        else
        {
            _stableTimer = 0;
        }
        
        // Earlier completion if most bodies are stable
        if (_bodiesAsleep > _physicsBodies.Count * 0.95f && _simulationTimer > 1.0f)
        {
            EmitSignal("SimulationCompleted");
            _simulationRunning = false;
        }
    }
    
    // Modified to take iteration count as parameter
    public List<Rect2I> SeparateCellsStep(List<Rect2I> cells, int iterations)
    {
        List<Rect2I> result = new List<Rect2I>(cells);
        
        // Run separation algorithm for specified number of iterations
        for (int iter = 0; iter < iterations; iter++)
        {
            bool anyCellMoved = false;
            
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
                    
                    anyCellMoved = true;
                }
            }
            
            // If no cell moved in this iteration, we can stop early
            if (!anyCellMoved)
                break;
        }
        
        return result;
    }
    
    [Signal]
    public delegate void SimulationCompletedEventHandler();
}