using NUnit.Framework;

// 스킬 쿨다운은 "준비 시각"(시뮬레이션 시계의 절대 시각)이다(docs/COMBAT_ARCHITECTURE.md 7-11).
// 시전할 때 한 번 계산해 저장하므로, 그 시각과 정확히 같은 시각에 준비된 것으로 판정된다(여유값 없음).
[TestFixture]
public class SkillCooldownTests
{
    [Test]
    public void NewSkill_IsReadyAtAnyTime()
    {
        Skill skill = TestSkills.Create(cooldown: 5f);
        Assert.IsTrue(skill.IsReadyAt(0.0));
        Assert.AreEqual(0.0, skill.RemainingCooldown(0.0));
        Assert.AreEqual(0f, skill.CooldownRatio(0.0));
    }

    [Test]
    public void Cooldown_StartsAtCastTime_AndEndsExactlyAtReadyAt()
    {
        Skill skill = TestSkills.Create(cooldown: 5f);
        skill.BeginCooldown(2.0);

        Assert.AreEqual(7.0, skill.ReadyAt);
        Assert.IsFalse(skill.IsReadyAt(6.999));
        Assert.IsTrue(skill.IsReadyAt(7.0));
        Assert.AreEqual(2.0, skill.RemainingCooldown(5.0), 1e-12);
        Assert.AreEqual(0.4f, skill.CooldownRatio(5.0), 1e-6f);
    }

    [Test]
    public void CastMidStep_ReadyAtExactlyStoredTime()
    {
        Skill skill = TestSkills.Create(cooldown: 1f);
        skill.BeginCooldown(1.013);

        double readyAt = skill.ReadyAt;
        Assert.IsTrue(skill.IsReadyAt(readyAt), "저장된 준비 시각 그대로 비교하면 정확히 준비");
        Assert.IsFalse(skill.IsReadyAt(readyAt - 1e-12));
    }

    [Test]
    public void RemainingCooldown_NeverNegative()
    {
        Skill skill = TestSkills.Create(cooldown: 5f);
        skill.BeginCooldown(0.0);

        Assert.AreEqual(0.0, skill.RemainingCooldown(100.0));
        Assert.AreEqual(0f, skill.CooldownRatio(100.0));
    }

    [Test]
    public void ZeroCooldown_IsReadyAtCastTime()
    {
        Skill skill = TestSkills.Create(cooldown: 0f);
        skill.BeginCooldown(3.0);

        Assert.IsTrue(skill.IsReadyAt(3.0));
        Assert.AreEqual(0f, skill.CooldownRatio(3.0));
    }
}
