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

    private static SkillLoadout NewLoadout() =>
        new SkillLoadout(5, id => id == 99 ? null : TestSkills.CreateData(id: id, cooldown: 10f));

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
