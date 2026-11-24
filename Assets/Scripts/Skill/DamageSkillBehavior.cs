using System.Collections.Generic;

// 단일 대상 데미지. 대상이 없으면 헛친다(맞은 대상 없음).
public class DamageSkillBehavior : ISkillBehavior
{
    private readonly float damageMultiplier;

    public DamageSkillBehavior(float damageMultiplier)
    {
        this.damageMultiplier = damageMultiplier;
    }

    public void Execute(Combatant caster, Combatant target, List<Combatant> hits)
    {
        if (target == null) return;

        target.TakeDamage(caster.ComputeDamage(damageMultiplier));
        hits.Add(target);
    }
}
