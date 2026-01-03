using Godot;
using System;

public partial class Player : CharacterBody2D
{
	private PackedScene playerBulletScene;
	[Export] private float speed = 200;
    private HealthComponent _healthComponent;
	
	public override void _Ready()
	{
        GameManager.Instance.RegisterPlayer(this);

		playerBulletScene = GD.Load<PackedScene>("res://Src/Projectiles/PlayerBullet/PlayerBullet.tscn");
        _healthComponent = GetNodeOrNull<HealthComponent>("HealthComponent");
        
        if (_healthComponent != null)
        {
            _healthComponent.HealthChanged += OnHealthChanged;
        }
	}

    private void OnHealthChanged(float newHealth, float changeAmount)
    {
        if (changeAmount < 0)
        {
            SignalManager.Instance.EmitSignal(nameof(SignalManager.PlayerTookDamage), Mathf.Abs(changeAmount));
        }
        else if (changeAmount > 0)
        {
            SignalManager.Instance.EmitSignal(nameof(SignalManager.PlayerHealed), changeAmount);
        }
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
            SignalManager.Instance.EmitSignal(nameof(SignalManager.WeaponFired));
		}
	}
}
