using Godot;

public partial class HealthComponent : Node2D
{

	[Export] public float MaxHealth = 100;
    [Export] public bool HasInvincibilityFrames = false;
    [Export] public float InvincibilityDuration = 0.5f;
	public float CurrentHealth;
	private ProgressBar _healthBar;
    private bool _isInvincible = false;
    private Timer _invincibilityTimer;

    [Signal]
    public delegate void DiedEventHandler();

    [Signal]
    public delegate void HealthChangedEventHandler(float newHealth, float changeAmount);

	public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        _healthBar = GetChild<ProgressBar>(0) ?? null;
        if (_healthBar != null)
        {
            _healthBar.MaxValue = MaxHealth;
            _healthBar.Value = CurrentHealth;
        }

        if (HasInvincibilityFrames)
        {
            _invincibilityTimer = new Timer();
            _invincibilityTimer.OneShot = true;
            _invincibilityTimer.WaitTime = InvincibilityDuration;
            _invincibilityTimer.Timeout += () => _isInvincible = false;
            AddChild(_invincibilityTimer);
        }
    }

	public void TakeDamage(float damage)
	{
        if (_isInvincible) return;

        if (HasInvincibilityFrames && _invincibilityTimer != null)
        {
            _isInvincible = true;
            _invincibilityTimer.Start();
        }

		CurrentHealth -= damage;
        EmitSignal(SignalName.HealthChanged, CurrentHealth, -damage);

		if (_healthBar != null)
		{
			_healthBar.Value = CurrentHealth;
		}
        if (CurrentHealth <= 0)
        {
            EmitSignal(SignalName.Died);
		}
	}

    public void Heal(float amount)
    {
        CurrentHealth = Mathf.Min(CurrentHealth + amount, MaxHealth);
        EmitSignal(SignalName.HealthChanged, CurrentHealth, amount);
        
        if (_healthBar != null)
        {
            _healthBar.Value = CurrentHealth;
        }
    }

    public void SetMaxHealth(float newMax, bool scaleCurrent = true)
    {
        float oldMax = MaxHealth;
        MaxHealth = newMax;
        
        if (scaleCurrent && oldMax > 0)
        {
            float ratio = CurrentHealth / oldMax;
            CurrentHealth = MaxHealth * ratio;
        }
        else
        {
            CurrentHealth = Mathf.Min(CurrentHealth, MaxHealth);
        }

        if (_healthBar != null)
        {
            _healthBar.MaxValue = MaxHealth;
            _healthBar.Value = CurrentHealth;
        }
    }
}