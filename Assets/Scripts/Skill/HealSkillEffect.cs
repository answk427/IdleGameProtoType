public class HealSkillEffect : ISkillEffect
{
    private readonly int healAmount;

    public HealSkillEffect(int healAmount)
    {
        this.healAmount = healAmount;
    }

    public void Execute(PlayerController caster, IDamageable target)
    {
        caster.Heal(healAmount);
    }
}
