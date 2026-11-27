using System.Collections.Generic;

// 시전자 회복.
public class HealSkillBehavior : ISkillBehavior
{
    private readonly GameNumber healAmount;

    public HealSkillBehavior(float healAmount)
    {
        this.healAmount = GameNumber.Round(healAmount);
    }

    public void Execute(Combatant caster, Combatant target, List<Combatant> hits)
    {
        caster.Heal(healAmount);
        hits.Add(caster);
    }
}
