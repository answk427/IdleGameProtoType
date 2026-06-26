public class DamageSkillBehavior : ISkillBehavior
{
    private readonly float damageMultiplier;

    public DamageSkillBehavior(float damageMultiplier)
    {
        this.damageMultiplier = damageMultiplier;
    }

    public UnityEngine.Vector3 Execute(ISkillCaster caster, IDamageable target)
    {
        target?.TakeDamage(caster.GetCalculatedDamage(damageMultiplier));
        return target != null ? target.Position : caster.Position;
    }
}
