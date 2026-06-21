public class Skill
{
    public SkillData Data { get; }
    public SkillEntry Entry { get; }

    private float currentCooldown;
    private readonly ISkillEffect effect;

    public Skill(SkillEntry entry)
    {
        Entry = entry;
        Data = entry.data;
        effect = CreateEffect(Data);
    }

    public bool IsReady => currentCooldown <= 0f;

    public void Tick(float deltaTime)
    {
        if (currentCooldown > 0f)
            currentCooldown -= deltaTime;
    }

    public void Use(PlayerController caster, IDamageable target)
    {
        currentCooldown = Data.cooldown;
        effect.Execute(caster, target);
    }

    private static ISkillEffect CreateEffect(SkillData data)
    {
        switch (data.effectType)
        {
            case SkillEffectType.Damage:
                return new DamageSkillEffect(data.value1);
            case SkillEffectType.AoeDamage:
                return new AoeDamageSkillEffect(data.value1, data.value2);
            case SkillEffectType.Heal:
                return new HealSkillEffect((int)data.value1);
            default:
                throw new System.ArgumentException($"Unknown skill effect type: {data.effectType}");
        }
    }
}
