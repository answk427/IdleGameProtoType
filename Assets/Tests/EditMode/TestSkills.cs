using Newtonsoft.Json;

// 테스트용 스킬 생성. 시뮬레이션은 사양(SkillSpec)을, 진행도·UI는 데이터 행(SkillData)을 쓴다.
// SkillData는 private setter라 엑셀 변환과 같은 경로(JSON)로 만든다.
public static class TestSkills
{
    public static Skill Create(int id = 1, float cooldown = 5f, string actionKey = "Skill", SkillEffectType effectType = SkillEffectType.Damage, float castTime = 0.5f)
    {
        return new Skill(CreateSpec(id, cooldown, actionKey, effectType, castTime));
    }

    // 데이터 행의 기본값(이름 "Test Skill", 값 1·0)과 같은 사양
    public static SkillSpec CreateSpec(int id = 1, float cooldown = 5f, string actionKey = "Skill", SkillEffectType effectType = SkillEffectType.Damage, float castTime = 0.5f) =>
        new SkillSpec(id, "Test Skill", cooldown, castTime, actionKey, effectType, 1f, 0f);

    // UI(아이콘을 쓰는 쪽)용 표 항목. 시뮬레이션·진행도는 데이터 행(CreateData)만 쓴다.
    public static SkillEntry CreateEntry(int id = 1, float cooldown = 5f, string actionKey = "Skill", SkillEffectType effectType = SkillEffectType.Damage, float castTime = 0.5f, int requiredLevel = 1) =>
        new SkillEntry { data = CreateData(id, cooldown, actionKey, effectType, castTime, requiredLevel) };

    public static SkillData CreateData(int id = 1, float cooldown = 5f, string actionKey = "Skill", SkillEffectType effectType = SkillEffectType.Damage, float castTime = 0.5f, int requiredLevel = 1)
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
            $"\"requiredLevel\":{requiredLevel}" +
            "}";
        return JsonConvert.DeserializeObject<SkillData>(json);
    }
}
