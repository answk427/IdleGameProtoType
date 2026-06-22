public class HealSkillBehavior : ISkillBehavior
{
    private readonly int healAmount;

    public HealSkillBehavior(int healAmount)
    {
        this.healAmount = healAmount;
    }

    public UnityEngine.Vector3 Execute(PlayerController caster, IDamageable target)
    {
        caster.Heal(healAmount);
        return caster.transform.position;
    }
}
