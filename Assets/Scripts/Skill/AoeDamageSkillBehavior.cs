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

    public UnityEngine.Vector3 Execute(ISkillCaster caster, IDamageable target)
    {
        List<IDamageable> targets = caster.GetOpponentsInRange(radius);
        int damage = caster.GetCalculatedDamage(damageMultiplier);

        for (int i = 0; i < targets.Count; i++)
        {
            targets[i].TakeDamage(damage);
        }

        // 범위 공격은 캐스터를 중심으로 퍼지므로 캐스터 위치를 이펙트 기준점으로 삼는다.
        return caster.Position;
    }
}
