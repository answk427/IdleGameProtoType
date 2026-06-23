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
    public int Key => _id;

    [UnityEngine.SerializeField] private int _id;
    [UnityEngine.SerializeField] private string _skillName;
    [UnityEngine.SerializeField] private float _cooldown;
    [UnityEngine.SerializeField] private string _animationTrigger;
    [UnityEngine.SerializeField] private SkillEffectType _effectType;
    [UnityEngine.SerializeField] private float _value1; // damageMultiplier 또는 healAmount
    [UnityEngine.SerializeField] private float _value2; // radius 등 (선택적)
    [UnityEngine.SerializeField] private int _requiredLevel; // 스킬 학습에 필요한 플레이어 레벨

    [JsonProperty("id")]
    public int id { get => _id; private set => _id = value; }
    [JsonProperty("skillName")]
    public string skillName { get => _skillName; private set => _skillName = value; }
    [JsonProperty("cooldown")]
    public float cooldown { get => _cooldown; private set => _cooldown = value; }
    [JsonProperty("animationTrigger")]
    public string animationTrigger { get => _animationTrigger; private set => _animationTrigger = value; }
    [JsonProperty("effectType")]
    public SkillEffectType effectType { get => _effectType; private set => _effectType = value; }
    [JsonProperty("value1")]
    public float value1 { get => _value1; private set => _value1 = value; }
    [JsonProperty("value2")]
    public float value2 { get => _value2; private set => _value2 = value; }
    [JsonProperty("requiredLevel")]
    public int requiredLevel { get => _requiredLevel; private set => _requiredLevel = value; }
}
