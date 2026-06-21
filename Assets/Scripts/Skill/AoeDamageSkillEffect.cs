using System.Collections.Generic;

public class AoeDamageSkillEffect : ISkillEffect
{
    private readonly float damageMultiplier;
    private readonly float radius;

    public AoeDamageSkillEffect(float damageMultiplier, float radius)
    {
        this.damageMultiplier = damageMultiplier;
        this.radius = radius;
    }

    public void Execute(PlayerController caster, IDamageable target)
    {
        List<Monster> monsters = GameManager.Instance.GetMonstersInRange(caster.transform.position.x, radius);
        int damage = caster.GetCalculatedDamage(damageMultiplier);

        for (int i = 0; i < monsters.Count; i++)
        {
            monsters[i].TakeDamage(damage);
        }
    }
}
