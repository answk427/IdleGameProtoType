using System.Collections.Generic;

public class AoeDamageSkillBehavior : ISkillBehavior
{
    private readonly float damageMultiplier;
    private readonly float radius;

    public AoeDamageSkillBehavior(float damageMultiplier, float radius)
    {
        this.damageMultiplier = damageMultiplier;
        this.radius = radius;
    }

    public UnityEngine.Vector3[] Execute(ISkillCaster caster, IDamageable target)
    {
        List<IDamageable> targets = caster.GetOpponentsInRange(radius);
        int damage = caster.GetCalculatedDamage(damageMultiplier);

        UnityEngine.Vector3[] hitPositions = new UnityEngine.Vector3[targets.Count];
        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].TakeDamage(damage);
            hitPositions[i] = targets[i].Position;
        }

        return hitPositions;
    }
}
