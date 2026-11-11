public interface IDamageable
{
    bool IsAlive { get; }

    void ApplyDamage(DamageInfo damageInfo);
}