public class Skill
{
    public SkillData Data { get; }
    public SkillEntry Entry { get; }

    private float currentCooldown;
    private readonly ISkillBehavior behavior;

    public Skill(SkillEntry entry)
    {
        Entry = entry;
        Data = entry.data;
        behavior = CreateBehavior(Data);
    }

    public bool IsReady => currentCooldown <= 0f;
    public float CooldownRatio => Data.Cooldown > 0f ? UnityEngine.Mathf.Clamp01(currentCooldown / Data.Cooldown) : 0f;

    public void Tick(float deltaTime)
    {
        if (currentCooldown > 0f)
            currentCooldown -= deltaTime;
    }

    public UnityEngine.Vector3 Use(PlayerController caster, IDamageable target)
    {
        currentCooldown = Data.Cooldown;
        return behavior.Execute(caster, target);
    }

    private static ISkillBehavior CreateBehavior(SkillData data)
    {
        switch (data.EffectType)
        {
            case SkillEffectType.Damage:
                return new DamageSkillBehavior(data.Value1);
            case SkillEffectType.AoeDamage:
                return new AoeDamageSkillBehavior(data.Value1, data.Value2);
            case SkillEffectType.Heal:
                return new HealSkillBehavior((int)data.Value1);
            default:
                throw new System.ArgumentException($"Unknown skill effect type: {data.EffectType}");
        }
    }
}
