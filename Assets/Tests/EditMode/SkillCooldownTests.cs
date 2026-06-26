using System.Reflection;
using NUnit.Framework;
using Newtonsoft.Json;

[TestFixture]
public class SkillCooldownTests
{
    private static readonly FieldInfo CooldownField =
        typeof(Skill).GetField("currentCooldown", BindingFlags.NonPublic | BindingFlags.Instance);

    private Skill CreateSkill(float cooldown, SkillEffectType effectType = SkillEffectType.Damage)
    {
        string json = "{" +
            "\"id\":1," +
            "\"skillName\":\"Test Skill\"," +
            $"\"cooldown\":{cooldown}," +
            "\"animationTrigger\":\"Skill\"," +
            $"\"effectType\":{(int)effectType}," +
            "\"value1\":1.0," +
            "\"value2\":0.0," +
            "\"requiredLevel\":1" +
            "}";

        SkillData data = JsonConvert.DeserializeObject<SkillData>(json);
        SkillEntry entry = new SkillEntry { data = data };
        return new Skill(entry);
    }

    // Skill.Use()는 PlayerController/IDamageable 실행까지 거치므로, 쿨다운 상태 전이만
    // 떼어서 검증하기 위해 private 필드를 직접 조작해 "방금 사용한 직후" 상태를 재현한다.
    private void SimulateUse(Skill skill)
    {
        float cooldown = skill.Data.Cooldown;
        CooldownField.SetValue(skill, cooldown);
    }

    [Test]
    public void NewSkill_IsReadyImmediately()
    {
        Skill skill = CreateSkill(5f);
        Assert.IsTrue(skill.IsReady);
        Assert.AreEqual(0f, skill.CooldownRatio);
    }

    [Test]
    public void AfterUse_IsNotReady_UntilCooldownElapses()
    {
        Skill skill = CreateSkill(5f);
        SimulateUse(skill);

        Assert.IsFalse(skill.IsReady);
        Assert.AreEqual(1f, skill.CooldownRatio, 0.0001f);

        skill.Tick(2f);
        Assert.IsFalse(skill.IsReady);
        Assert.AreEqual(0.6f, skill.CooldownRatio, 0.0001f);

        skill.Tick(3f);
        Assert.IsTrue(skill.IsReady);
        Assert.AreEqual(0f, skill.CooldownRatio, 0.0001f);
    }

    [Test]
    public void Tick_Overshoot_DoesNotProduceNegativeRatio()
    {
        Skill skill = CreateSkill(5f);
        SimulateUse(skill);

        // 쿨다운보다 큰 deltaTime이 한 번에 들어와도 비율이 음수로 새지 않아야 한다.
        skill.Tick(100f);

        Assert.IsTrue(skill.IsReady);
        Assert.AreEqual(0f, skill.CooldownRatio);
    }

    [Test]
    public void Tick_WhileAlreadyReady_StaysReady()
    {
        Skill skill = CreateSkill(5f);

        skill.Tick(1f);

        Assert.IsTrue(skill.IsReady);
    }

    [Test]
    public void ZeroCooldownSkill_RatioIsAlwaysZero()
    {
        Skill skill = CreateSkill(0f);
        SimulateUse(skill);

        Assert.AreEqual(0f, skill.CooldownRatio);
    }
}
