using Godot;
using System;

public partial class EnemyRed : Enemy
{
    [Export] public float AggroRange = 200f; // Distance to notice player
    [Export] public float DeaggroRange = 800f; // Distance to give up (much larger than Aggro)

    private Player player;
    private Vector2[] _currentPath;
    private int _currentPathIndex = 0;
    private double _pathRefreshTimer = 0;
    private const double PathRefreshInterval = 0.2;
    private bool _isAggroed = false;

    public override void _Ready()
    {
        base._Ready();
        // Set default values if needed, or rely on Inspector
        // Speed and Damage are now in base Enemy
        
        player = GetNodeOrNull<Player>("/root/Level/Player");
    }

    public override void _Process(double delta)
    {
        base._Process(delta);
        if (GameManager.Instance.ShowEnemyDebug)
        {
            QueueRedraw();
        }
    }

    public override void _Draw()
    {
        base._Draw();
        if (!GameManager.Instance.ShowEnemyDebug) return;

        // Draw Aggro Range (Yellow)
        DrawArc(Vector2.Zero, AggroRange, 0, Mathf.Tau, 32, Colors.Yellow, 1.0f);
        
        // Draw Deaggro Range (Red dashed-ish)
        DrawArc(Vector2.Zero, DeaggroRange, 0, Mathf.Tau, 32, Colors.Red, 1.0f);

        // Draw Path
        if (_currentPath != null && _currentPath.Length > 0)
        {
            // Draw line to first point
            if (_currentPathIndex < _currentPath.Length)
            {
                DrawLine(Vector2.Zero, ToLocal(_currentPath[_currentPathIndex]), Colors.Cyan, 2.0f);
            }

            // Draw remaining path
            for (int i = _currentPathIndex; i < _currentPath.Length - 1; i++)
            {
                DrawLine(ToLocal(_currentPath[i]), ToLocal(_currentPath[i+1]), Colors.Cyan, 2.0f);
            }
        }
        
        // Draw Line of Sight ray
        if (player != null)
        {
             Color losColor = HasLineOfSight() ? Colors.Green : Colors.Red;
             // Only draw if within reasonable range to avoid clutter
             if (GlobalPosition.DistanceTo(player.GlobalPosition) < DeaggroRange)
             {
                DrawLine(Vector2.Zero, ToLocal(player.GlobalPosition), losColor, 1.0f);
             }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        base._PhysicsProcess(delta);
        if (player == null) return;

        float distToPlayer = GlobalPosition.DistanceTo(player.GlobalPosition);

        // State Machine Logic
        if (!_isAggroed)
        {
            // Only aggro if close enough AND has line of sight
            if (distToPlayer <= AggroRange && HasLineOfSight())
            {
                _isAggroed = true;
                // Force immediate path update when spotted
                _pathRefreshTimer = 0; 
            }
        }
        else
        {
            // De-aggro only if player is extremely far away (or dead)
            if (distToPlayer > DeaggroRange)
            {
                _isAggroed = false;
                _currentPath = null;
            }
        }

        // Movement Logic
        if (_isAggroed)
        {
            _pathRefreshTimer -= delta;
            if (_pathRefreshTimer <= 0)
            {
                _pathRefreshTimer = PathRefreshInterval;
                UpdatePath();
            }
            FollowPath(delta);
        }
        else
        {
            _currentPath = null;
        }

        if (HitboxComponent != null)
        {
            if (OverlapsBody(player))
            {
                var playerHitbox = player.GetNodeOrNull<HitboxComponent>("HitboxComponent");
                if (playerHitbox != null)
                {
                    playerHitbox.Damage(Damage);
                }
            }
        }
    }

    private bool HasLineOfSight()
    {
        var spaceState = GetWorld2D().DirectSpaceState;
        var query = PhysicsRayQueryParameters2D.Create(GlobalPosition, player.GlobalPosition);
        
        // Exclude the enemy itself from the raycast so we don't hit our own collider
        query.Exclude = new Godot.Collections.Array<Rid> { GetRid() };
        
        // IntersectRay returns a dictionary. If it hits something, we check what it is.
        var result = spaceState.IntersectRay(query);

        if (result.Count > 0)
        {
            var collider = result["collider"].As<Node>();
            
            // If we hit the player, we have LoS.
            if (collider == player) return true;
            
            // If we hit a wall (TileMapLayer) or anything else before the player, we don't have LoS.
            return false;
        }

        // If ray hits nothing (rare, usually means player is outside physics world?), assume no LoS
        return true;
    }

    private void UpdatePath()
    {
        if (player == null) return;
        var newPath = EnemyManager.Instance.GetPath(GlobalPosition, player.GlobalPosition);
        
        if (newPath != null && newPath.Length > 0)
        {
            _currentPath = newPath;
            // Skip the first point (current cell center) if there is a next point
            // This prevents the enemy from walking back to the center of the current tile
            _currentPathIndex = (newPath.Length > 1) ? 1 : 0;
        }
    }

    private void FollowPath(double delta)
    {
        if (_currentPath == null || _currentPathIndex >= _currentPath.Length) return;
        
        Vector2 target = _currentPath[_currentPathIndex];
        Vector2 direction = (target - GlobalPosition).Normalized();
        Position += direction * Speed * (float)delta;
        
        if (GlobalPosition.DistanceTo(target) < 5f)
        {
            _currentPathIndex++;
        }
    }
}
