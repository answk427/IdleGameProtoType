using Newtonsoft.Json;

// 테스트용 스킬 생성. SkillData는 private setter라 엑셀 변환과 같은 경로(JSON)로 만든다.
public static class TestSkills
{
    public static Skill Create(int id = 1, float cooldown = 5f, string actionKey = "Skill", SkillEffectType effectType = SkillEffectType.Damage, float castTime = 0.5f)
    {
        return new Skill(CreateEntry(id, cooldown, actionKey, effectType, castTime));
    }

    public static SkillEntry CreateEntry(int id = 1, float cooldown = 5f, string actionKey = "Skill", SkillEffectType effectType = SkillEffectType.Damage, float castTime = 0.5f)
    {
        string json = "{" +
            $"\"id\":{id}," +
            "\"skillName\":\"Test Skill\"," +
            $"\"cooldown\":{cooldown.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"castTime\":{castTime.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
            $"\"animationTrigger\":\"{actionKey}\"," +
            $"\"effectType\":{(int)effectType}," +
            "\"value1\":1.0," +
            "\"value2\":0.0," +
            "\"requiredLevel\":1" +
            "}";
        SkillData data = JsonConvert.DeserializeObject<SkillData>(json);
        return new SkillEntry { data = data };
    }
}
