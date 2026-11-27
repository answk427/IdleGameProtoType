using System;

// 스킬 효과의 종류. 새 효과는 ISkillBehavior 구현 하나 + Skill.CreateBehavior 분기 하나를 더한다.
// 값은 데이터(.asset)에 정수로 저장되므로 번호를 바꾸지 않는다.
public enum SkillEffectType
{
    Damage,
    AoeDamage,
    Heal
}

// 시뮬레이션이 쓰는 스킬 값(docs/COMBAT_ARCHITECTURE.md 13-4). 순수 C#, 불변.
// 데이터 쪽(GameData)이 데이터 행(SkillData)에서 한 번 만든다. 학습 조건(필요 레벨)은 진행도의 값이라 없다.
// 그림 자산(아이콘, VFX, 소리)은 모른다: 뷰가 스킬 id로 Presentation에서 찾는다(12절 D47).
public sealed class SkillSpec
{
    public SkillSpec(int id, string name, float cooldown, float castTime, string actionKey, SkillEffectType effectType, float value1, float value2)
    {
        Id = id;
        Name = name;
        Cooldown = cooldown;
        CastTime = castTime;
        ActionKey = actionKey;
        EffectType = effectType;
        Value1 = value1;
        Value2 = value2;
    }

    public int Id { get; }
    public string Name { get; } // 오류 메시지용
    public float Cooldown { get; }
    // 시전 시간(초). 스킬 행동 한 번의 길이로, 시전자와 상관없이 이 값이다(7-13). 0 이하면 그 스킬은 쓰지 않고 에러를 낸다.
    public float CastTime { get; }
    // 시전자의 행동 시간표에서 찾을 행동 키(= 애니메이터 상태 이름). 데이터 행의 이름은 옛 그대로 animationTrigger다.
    public string ActionKey { get; }
    public SkillEffectType EffectType { get; }
    public float Value1 { get; } // 데미지 배율 또는 회복량
    public float Value2 { get; } // 반경 등(선택)

    public override string ToString() => $"스킬 {Id}({Name})";
}
