using Godot;

public partial class HealthComponent : Node2D
{

	[Export] public float MaxHealth = 100;
	public float CurrentHealth;
	private ProgressBar _healthBar;

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
    }

	public void TakeDamage(float damage)
	{
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
}