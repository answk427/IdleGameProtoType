using System;
using NUnit.Framework;
using static CombatTestKit;

// SimulationLoop(docs/COMBAT_ARCHITECTURE.md 11절 5-B): 화면 프레임의 게임 시간 → 고정 단계 몇 번 + 남은 비율.
// 한 프레임 상한, 잘못된 입력, 단계 밖 사건의 배달. 결과가 프레임 길이와 무관한지는 실제 데이터로 HeadlessGameTests가 본다.
[TestFixture]
public class SimulationLoopTests
{
    private static (SimulationLoop loop, CombatWorld world) NewLoop(double max = SimulationLoop.DefaultMaxFrameSeconds)
    {
        CombatWorld world = World();
        return (new SimulationLoop(world, null, max), world);
    }

    [Test]
    public void Advance_RunsWholeSteps_KeepsTheRemainder()
    {
        var (loop, world) = NewLoop();

        Assert.AreEqual(2, loop.Advance(0.05));
        Assert.AreEqual(2, world.Clock.StepIndex);
        Assert.AreEqual(0.01, loop.Pending, 1e-12);
        Assert.AreEqual(0.5f, loop.Alpha, 1e-6f);

        Assert.AreEqual(1, loop.Advance(0.01), "남은 0.01 + 0.01 = 한 단계");
        Assert.That(loop.Alpha, Is.InRange(0f, 1f));
        Assert.Less(loop.Alpha, 1f);
    }

    [Test]
    public void LongFrame_IsCappedAtMaxFrameSeconds()
    {
        var (loop, world) = NewLoop();

        int steps = loop.Advance(100.0); // 앱이 내려갔다 올라옴
        Assert.AreEqual((int)Math.Floor(SimulationLoop.DefaultMaxFrameSeconds / 0.02), steps, "상한(1/3초)만큼만, 넘은 시간은 버린다");
        Assert.Less(loop.Pending, 0.02);
    }

    [Test]
    public void ZeroNegativeAndNaN_RunNothing()
    {
        var (loop, world) = NewLoop();

        Assert.AreEqual(0, loop.Advance(0.0));
        Assert.AreEqual(0, loop.Advance(-1.0));
        Assert.AreEqual(0, loop.Advance(double.NaN));
        Assert.AreEqual(0, world.Clock.StepIndex);
        Assert.AreEqual(0.0, loop.Pending);
    }

    // 프레임이 어떻게 나뉘어도 같은 총 시간이면 단계 수가 같다(소수 오차로 경계에서 한 단계 늦을 수는 있다: 결과는 단계에만 달렸다).
    [Test]
    public void SplittingFrames_KeepsTheStepCount()
    {
        var (coarse, coarseWorld) = NewLoop();
        var (fine, fineWorld) = NewLoop();

        for (int i = 0; i < 30; i++) coarse.Advance(0.1);
        for (int i = 0; i < 3000; i++) fine.Advance(0.001);

        Assert.That(Math.Abs(coarseWorld.Clock.StepIndex - fineWorld.Clock.StepIndex), Is.LessThanOrEqualTo(1));
        Assert.That(coarseWorld.Clock.StepIndex, Is.InRange(149, 150));
    }

    // 단계 밖에서 생긴 사건(업그레이드로 바뀐 능력치 등)은 단계가 없는 프레임에도 배달된다.
    [Test]
    public void OutOfStepEvents_AreDelivered_EvenWithoutAStep()
    {
        var (loop, world) = NewLoop();
        var host = new FakeHost();
        Combatant c = Spawn(world, Team.Ally, 0f, 0.5f, Stats(hp: 100), host: host);

        c.UpdateStats(Stats(hp: 200));
        Assert.AreEqual(0, loop.Advance(0.001));
        Assert.AreEqual(1, host.Joined);
        Assert.AreEqual(1, host.StatsChanged);
    }

    [Test]
    public void InvalidFrameCap_IsRejected()
    {
        CombatWorld world = World();
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationLoop(world, null, 0.0));
        Assert.Throws<ArgumentOutOfRangeException>(() => new SimulationLoop(world, null, double.PositiveInfinity));
        Assert.Throws<ArgumentNullException>(() => new SimulationLoop(null, null));
    }
}
