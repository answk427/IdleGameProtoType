using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// 스킬 쿨다운은 슬롯이 아니라 스킬 id에 붙는다(docs/COMBAT_ARCHITECTURE.md 7-6).
// 예전 버그: 스킬을 다른 슬롯으로 옮기거나 풀었다 다시 끼우면 쿨다운이 0이 됐다.
[TestFixture]
public class SkillLoadoutTests
{
    private const int Empty = SkillLoadout.EmptySlot;

    // GameData처럼 id마다 사양 하나를 만들어 둔다(99는 데이터에 없는 스킬).
    private static SkillLoadout NewLoadout()
    {
        var specs = new System.Collections.Generic.Dictionary<int, SkillSpec>();
        return new SkillLoadout(5, id =>
        {
            if (id == 99) return null;
            if (!specs.TryGetValue(id, out SkillSpec spec)) specs[id] = spec = TestSkills.CreateSpec(id: id, cooldown: 10f);
            return spec;
        });
    }

    // 데이터가 바뀌면(튜닝 덮어쓰기, 13-4) 장착을 다시 맞출 때 같은 스킬 객체가 새 값으로 바뀐다. 준비 시각은 그대로이고 새 쿨다운은 다음 시전부터.
    [Test]
    public void ChangedSpec_IsSwappedInOnSetSlots_KeepingReadyTime()
    {
        SkillSpec current = TestSkills.CreateSpec(id: 1, cooldown: 10f);
        var loadout = new SkillLoadout(5, id => id == 1 ? current : null);
        loadout.SetSlots(new[] { 1, Empty, Empty, Empty, Empty });
        Skill skill = loadout.GetSlot(0);
        skill.BeginCooldown(0.0);

        current = TestSkills.CreateSpec(id: 1, cooldown: 3f);
        loadout.SetSlots(new[] { 1, Empty, Empty, Empty, Empty });

        Assert.AreSame(skill, loadout.GetSlot(0), "같은 스킬 객체(쿨다운 상태)를 쓴다");
        Assert.AreSame(current, skill.Spec);
        Assert.AreEqual(10.0, skill.ReadyAt, "이미 시작한 쿨다운은 옛 값 그대로");
        skill.BeginCooldown(20.0);
        Assert.AreEqual(23.0, skill.ReadyAt, "다음 시전부터 새 쿨다운");
    }

    [Test]
    public void MovingSkillToAnotherSlot_KeepsCooldown()
    {
        SkillLoadout loadout = NewLoadout();
        loadout.SetSlots(new[] { 1, Empty, Empty, Empty, Empty });
        loadout.GetSlot(0).BeginCooldown(0.0);

        loadout.SetSlots(new[] { Empty, Empty, 1, Empty, Empty });

        Assert.IsNull(loadout.GetSlot(0));
        Assert.AreEqual(10.0, loadout.GetSlot(2).ReadyAt);
        Assert.IsFalse(loadout.GetSlot(2).IsReadyAt(3.0));
    }

    [Test]
    public void UnequipAndReequip_KeepsSameInstance_AndCooldownKeepsRunning()
    {
        SkillLoadout loadout = NewLoadout();
        loadout.SetSlots(new[] { 1, Empty, Empty, Empty, Empty });
        Skill skill = loadout.GetSlot(0);
        skill.BeginCooldown(0.0);

        loadout.SetSlots(new[] { Empty, Empty, Empty, Empty, Empty });
        loadout.SetSlots(new[] { Empty, 1, Empty, Empty, Empty });

        Assert.AreSame(skill, loadout.GetSlot(1), "같은 스킬 인스턴스를 다시 쓴다");
        // 준비 시각은 절대 시각이라, 장착하지 않은 동안에도 시간은 흘렀다(결정 S2).
        Assert.IsTrue(loadout.GetSlot(1).IsReadyAt(10.0));
    }

    [Test]
    public void PickReady_UsesSlotOrderAsPriority_AtExactTime()
    {
        SkillLoadout loadout = NewLoadout();
        loadout.SetSlots(new[] { 1, 2, Empty, Empty, Empty });
        loadout.GetSlot(0).BeginCooldown(0.0); // 10초에 준비

        Assert.AreSame(loadout.GetSlot(1), loadout.PickReady(9.999));
        Assert.AreSame(loadout.GetSlot(0), loadout.PickReady(10.0), "준비되는 그 시각부터 우선순위가 높은 슬롯을 쓴다");
    }

    [Test]
    public void NextReadyTime_IsEarliestFutureReadyAtOfEquippedSkills()
    {
        SkillLoadout loadout = NewLoadout();
        loadout.SetSlots(new[] { 1, 2, 3, Empty, Empty });
        loadout.GetSlot(0).BeginCooldown(5.0);  // 15
        loadout.GetSlot(1).BeginCooldown(2.0);  // 12
        // 슬롯 2는 한 번도 안 써서 이미 준비(미래 시각이 아님)

        Assert.AreEqual(12.0, loadout.NextReadyTime(3.0));
        Assert.AreEqual(15.0, loadout.NextReadyTime(12.0), "지금 시각과 같은 준비 시각은 미래가 아니다");

        loadout.SetSlots(new[] { Empty, Empty, Empty, Empty, Empty });
        Assert.AreEqual(double.PositiveInfinity, loadout.NextReadyTime(3.0), "장착하지 않은 스킬은 결정 입력이 아니다");
    }

    [Test]
    public void ShortSlotList_LeavesRestEmpty()
    {
        SkillLoadout loadout = NewLoadout();
        loadout.SetSlots(new[] { 1 });

        Assert.IsNotNull(loadout.GetSlot(0));
        Assert.IsNull(loadout.GetSlot(4));
        Assert.IsNull(loadout.GetSlot(-1));
        Assert.IsNull(loadout.GetSlot(5));
    }

    [Test]
    public void UnknownSkill_LeavesSlotEmpty_AndReports()
    {
        SkillLoadout loadout = NewLoadout();

        LogAssert.Expect(LogType.Error, new Regex("스킬 99의 데이터가 없습니다"));
        loadout.SetSlots(new[] { 99, Empty, Empty, Empty, Empty });

        Assert.IsNull(loadout.GetSlot(0));
    }
}
