using System.Collections.Generic;

// 범위 데미지. 반경은 시전자의 몸 표면에서 상대의 몸 표면까지의 거리다(D10, 교전·대상과 같은 거리 규칙).
public class AoeDamageSkillBehavior : ISkillBehavior
{
    private readonly float damageMultiplier;
    private readonly float radius;

    public AoeDamageSkillBehavior(float damageMultiplier, float radius)
    {
        this.damageMultiplier = damageMultiplier;
        this.radius = radius;
    }

    public void Execute(Combatant caster, Combatant target, List<Combatant> hits)
    {
        if (caster.World == null) return;

        int first = hits.Count;
        caster.World.CollectOpponentsInRange(caster, radius, hits);

        GameNumber damage = caster.ComputeDamage(damageMultiplier);
        for (int i = first; i < hits.Count; i++)
        {
            hits[i].TakeDamage(damage);
        }
    }
}
