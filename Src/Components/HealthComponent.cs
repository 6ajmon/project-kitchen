using Godot;

public partial class HealthComponent : Node2D
{

	[Export] public float MaxHealth = 100;
	public float CurrentHealth;
	private ProgressBar _healthBar;

    [Signal]
    public delegate void DiedEventHandler();

	public override void _Ready()
    {
        CurrentHealth = MaxHealth;
        _healthBar = GetChild<ProgressBar>(0);
        _healthBar.MaxValue = MaxHealth;
        _healthBar.Value = CurrentHealth;
    }

	public void TakeDamage(float damage)
	{
		CurrentHealth -= damage;
		if (_healthBar != null)
		{
			_healthBar.Value = CurrentHealth;
		}
        if (CurrentHealth <= 0)
        {
            EmitSignal(SignalName.Died);
		}
	}
}