// 스킬 하나의 런타임 상태(쿨다운)와 효과. 밸런스 수치는 SkillData, 타이밍은 시전자의 행동 시간표가 갖는다.
//
// 쿨다운은 남은 시간이 아니라 "준비 시각"(시뮬레이션 시계의 절대 시각)으로 갖는다(docs/COMBAT_ARCHITECTURE.md 7-11).
// 시전할 때 한 번 계산해 저장하므로(시전 시각 + 쿨다운), 스케줄러가 그 시각으로 시계를 옮기면 정확히 준비된 것으로 판정된다.
// 시간이 흐르는 동안 값을 깎을 필요가 없어서 Tick이 없다. 장착 여부와 상관없이 시간은 흐른다.
public class Skill
{
    public SkillData Data { get; }
    public SkillEntry Entry { get; }

    private readonly ISkillBehavior behavior;

    public Skill(SkillEntry entry)
    {
        Entry = entry;
        Data = entry.data;
        behavior = CreateBehavior(Data);
    }

    // 이 시각부터 쓸 수 있다. 한 번도 쓰지 않았으면 언제든 쓸 수 있다.
    public double ReadyAt { get; private set; } = double.NegativeInfinity;

    public bool IsReadyAt(double now) => now >= ReadyAt;

    // 시전을 시작하는 순간 쿨다운이 시작된다.
    public void BeginCooldown(double castTime)
    {
        ReadyAt = castTime + Data.Cooldown;
    }

    public double RemainingCooldown(double now) => ReadyAt > now ? ReadyAt - now : 0.0;

    public float CooldownRatio(double now) =>
        Data.Cooldown > 0f ? UnityEngine.Mathf.Clamp01((float)(RemainingCooldown(now) / Data.Cooldown)) : 0f;

    // 타격 표식마다 한 번 부른다. 반환값은 효과가 실제로 적용된 위치들(이펙트 기준점).
    public UnityEngine.Vector3[] Apply(ISkillCaster caster, IDamageable target)
    {
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
