using System.ComponentModel;

public interface IDamageSource
{
    public void OnScoreKill(IDamageable target);
}