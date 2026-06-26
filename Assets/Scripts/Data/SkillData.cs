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
