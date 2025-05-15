using Godot;
public partial class HitboxComponent : Area2D
{
	[Export] public HealthComponent HealthComponent;
	
	public void Damage(float damage)
	{
		HealthComponent?.TakeDamage(damage);
	}
}