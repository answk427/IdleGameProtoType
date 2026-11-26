using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

// 정적 버스(GlobalCombatEvents, docs/UI_DESIGN.md 5-3): 플레이를 시작할 때 구독자를 비운다(지난 플레이의 파괴된 구독자에게 가지 않게).
// 내용이 구조체라 발행에 할당이 없다.
[TestFixture]
public class GlobalCombatEventsTests
{
    [TearDown]
    public void TearDown() => GlobalCombatEvents.ResetSubscribers();

    [Test]
    public void ResetSubscribers_DropsEarlierSubscribers()
    {
        int before = 0, after = 0;
        GlobalCombatEvents.TargetDamaged.Subscribe(_ => before++);

        GlobalCombatEvents.ResetSubscribers();
        GlobalCombatEvents.TargetDamaged.Subscribe(_ => after++);
        GlobalCombatEvents.RaiseTargetDamaged(null, (GameNumber)5, Vector3.zero);

        Assert.AreEqual(0, before, "지난 플레이의 구독자에게는 가지 않는다");
        Assert.AreEqual(1, after);
    }

    [Test]
    public void Raise_DeliversPayload_AndAllocatesNothing()
    {
        GameNumber total = GameNumber.Zero;
        GlobalCombatEvents.Healed.Subscribe(e => total += e.Amount);
        GlobalCombatEvents.RaiseHealed(null, (GameNumber)1, Vector3.zero); // 첫 실행의 일회성 할당을 빼고 잰다

        Assert.That(() =>
        {
            for (int i = 0; i < 100; i++) GlobalCombatEvents.RaiseHealed(null, (GameNumber)1, Vector3.one);
        }, Is.Not.AllocatingGCMemory());
        Assert.AreEqual((GameNumber)101, total);
    }
}
