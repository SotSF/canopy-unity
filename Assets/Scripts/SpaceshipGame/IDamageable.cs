using System.ComponentModel;

public interface IDamageable
{
    public void TakeDamage(float damage, IDamageSource source);
}