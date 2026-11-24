using Newtonsoft.Json;

public enum SkillEffectType
{
    Damage,
    AoeDamage,
    Heal
}

[System.Serializable]
public class SkillData : IData
{
    public int Key => id;

    [UnityEngine.SerializeField] private int id;
    [UnityEngine.SerializeField] private string skillName;
    [UnityEngine.SerializeField] private float cooldown;
    // 시전 시간(초). 스킬 행동 한 번의 길이로, 시전자와 상관없이 이 값이다. 애니메이션은 이 길이에 맞춰 재생되고,
    // 타격은 시전자 클립의 타격 비율 지점에 들어간다(docs/COMBAT_ARCHITECTURE.md 7-13). 0 이하면 그 스킬은 쓰지 않고 에러를 낸다.
    // 원본은 DataTables/GameData.xlsx의 SkillDatabase 시트다(열이 빠지면 변환기가 거부한다).
    [UnityEngine.SerializeField] private float castTime;
    // 이름은 옛 그대로지만(직렬화 함정·엑셀 열 이름 때문에 바꾸지 않음) 이제 트리거가 아니라
    // "시전자의 행동 시간표에서 찾을 행동 키"(= 애니메이터 상태 이름)다. 타격 비율은 시전자의 ActionTimelineSet이 갖는다.
    [UnityEngine.SerializeField] private string animationTrigger;
    [UnityEngine.SerializeField] private SkillEffectType effectType;
    [UnityEngine.SerializeField] private float value1; // damageMultiplier 또는 healAmount
    [UnityEngine.SerializeField] private float value2; // radius 등 (선택적)
    [UnityEngine.SerializeField] private int requiredLevel; // 스킬 학습에 필요한 플레이어 레벨

    [JsonProperty("id")]
    public int Id { get => id; private set => id = value; }
    [JsonProperty("skillName")]
    public string SkillName { get => skillName; private set => skillName = value; }
    [JsonProperty("cooldown")]
    public float Cooldown { get => cooldown; private set => cooldown = value; }
    [JsonProperty("castTime")]
    public float CastTime { get => castTime; private set => castTime = value; }
    [JsonProperty("animationTrigger")]
    public string AnimationTrigger { get => animationTrigger; private set => animationTrigger = value; }
    [JsonProperty("effectType")]
    public SkillEffectType EffectType { get => effectType; private set => effectType = value; }
    [JsonProperty("value1")]
    public float Value1 { get => value1; private set => value1 = value; }
    [JsonProperty("value2")]
    public float Value2 { get => value2; private set => value2 = value; }
    [JsonProperty("requiredLevel")]
    public int RequiredLevel { get => requiredLevel; private set => requiredLevel = value; }
}
