using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using static CombatTestKit;

// HP·데미지 GameNumber(D5, D14)와 생애(사망, 부활, 능력치 갱신)의 불변식. 극단값으로 확인한다.
[TestFixture]
public class CombatantHealthTests
{
    private static Combatant Lone(double hp, FakeHost host) => Spawn(World(), Team.Enemy, 0f, 0.5f, Stats(hp: hp), host: host);

    [Test]
    public void HugeValues_StayExact_AndNeverGoBelowZero()
    {
        // 2^100(약 1.27e30)처럼 정확히 표현되는 값으로 비교한다.
        double big = System.Math.Pow(2, 100);
        var host = new FakeHost();
        Combatant c = Lone(big, host);

        c.TakeDamage(GameNumber.FromDouble(System.Math.Pow(2, 98)));
        Assert.AreEqual(GameNumber.FromDouble(3 * System.Math.Pow(2, 98)), c.Hp);
        Assert.IsTrue(c.IsAlive);

        c.TakeDamage(GameNumber.FromDouble(1e31));
        Assert.AreEqual(GameNumber.Zero, c.Hp);
        Assert.IsFalse(c.IsAlive);
        Assert.AreEqual(1, host.Died);
    }

    [Test]
    public void NegativeOrNaNDamage_IsTreatedAsZero()
    {
        var host = new FakeHost();
        Combatant c = Lone(100, host);

        c.TakeDamage(GameNumber.FromDouble(-50));
        c.TakeDamage(GameNumber.FromDouble(double.NaN));

        Assert.AreEqual((GameNumber)100, c.Hp);
        Assert.AreEqual(0, host.Died);
    }

    [Test]
    public void Heal_IsCappedAtMax_AndIgnoredWhenDead()
    {
        var host = new FakeHost();
        Combatant c = Lone(100, host);
        c.TakeDamage(30);
        c.Heal(GameNumber.FromDouble(1e30));
        Assert.AreEqual((GameNumber)100, c.Hp);

        c.TakeDamage(1000);
        int healedBefore = host.Healed;
        c.Heal(50);
        Assert.AreEqual(GameNumber.Zero, c.Hp);
        Assert.AreEqual(healedBefore, host.Healed);
    }

    [Test]
    public void DeathHappensOnce_AndLaterDamageIsIgnored()
    {
        var host = new FakeHost();
        Combatant c = Lone(10, host);

        c.TakeDamage(10);
        c.TakeDamage(10);

        Assert.AreEqual(1, host.Died);
        Assert.AreEqual(1, host.Damaged);
    }

    [Test]
    public void Revive_RestoresFullHp_AndMovingPhase()
    {
        var host = new FakeHost();
        Combatant c = Lone(80, host);
        c.TakeDamage(80);

        c.Revive();

        Assert.AreEqual((GameNumber)80, c.Hp);
        Assert.AreEqual(CombatantPhase.Moving, c.Phase);
        Assert.AreEqual(1, host.Revived);
    }

    [Test]
    public void UpdateStats_KeepsCurrentHp_ButNotAboveNewMax()
    {
        Combatant c = Lone(100, new FakeHost());
        c.TakeDamage(20);

        c.UpdateStats(Stats(hp: 200));
        Assert.AreEqual((GameNumber)80, c.Hp, "최대치가 늘어도 현재 HP는 그대로");

        c.UpdateStats(Stats(hp: 50));
        Assert.AreEqual((GameNumber)50, c.Hp);
    }

    [Test]
    public void ComputeDamage_RoundsToWholeNumber_ForHugeAttack()
    {
        CombatWorld world = World();
        Combatant c = Spawn(world, Team.Ally, 0f, 0.5f, Stats(damage: 3e20));

        Assert.AreEqual(GameNumber.Round(3e20 * 2.5), c.ComputeDamage(2.5));
        Assert.AreEqual((GameNumber)25, Spawn(world, Team.Ally, 0f, 0.5f, Stats(damage: 10)).ComputeDamage(2.5));
    }

    [Test]
    public void InvalidStats_AreReported_AndClampedSoProgressContinues()
    {
        LogAssert.Expect(LogType.Error, new System.Text.RegularExpressions.Regex("최대 HP가 잘못됐습니다"));
        Combatant c = Lone(0, new FakeHost());

        Assert.AreEqual((GameNumber)1, c.MaxHp);
        Assert.IsTrue(c.IsAlive);
    }
}
