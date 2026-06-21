public class DamageSkillEffect : ISkillEffect
{
    private readonly float damageMultiplier;

    public DamageSkillEffect(float damageMultiplier)
    {
        this.damageMultiplier = damageMultiplier;
    }

    public void Execute(PlayerController caster, IDamageable target)
    {
        target?.TakeDamage(caster.GetCalculatedDamage(damageMultiplier));
    }
}
