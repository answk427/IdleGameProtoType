using System;
using System.Collections.Generic;
using NUnit.Framework;

// ActionRunner의 불변식(docs/COMBAT_ARCHITECTURE.md 7-3, 7-11, 7-13).
// 표식 시각은 시작할 때 한 번 계산해 저장하므로, 그 시각으로 처리하면 정확히 같은 값으로 처리된다(여유값 없음).
// 표식은 타격과 완료뿐이다. 행동은 끝까지 진행되고 도중에 멈추는 것은 강제 중단(Cancel)뿐이다.
[TestFixture]
public class ActionRunnerTests
{
    private sealed class Recorder : IActionListener
    {
        public readonly List<string> Order = new List<string>();
        public Action OnHitCallback;

        public void OnActionHit(ActionTiming action, int hitIndex)
        {
            Order.Add("hit" + hitIndex);
            OnHitCallback?.Invoke();
        }

        public void OnActionCompleted(ActionTiming action) => Order.Add("complete");
    }

    // 지금 플레이어 기본 공격 클립과 같은 비율(1.2초 클립, 타격 0.7초)
    private static ActionTiming Attack() => ActionTiming.FromValues("Attack", 1.2f, new[] { 0.7f / 1.2f });

    [Test]
    public void MarkerTimes_AreStoredOnce_AndFireExactlyAtThoseTimes()
    {
        ActionTiming attack = Attack();
        var runner = new ActionRunner();
        var recorder = new Recorder();
        runner.Start(attack, 1.0, 5.0);

        double hitTime = 5.0 + attack.HitFractions[0] * 1.0;
        Assert.AreEqual(hitTime, runner.NextMarkerTime);

        runner.FireMarkersUpTo(hitTime, recorder); // 바로 그 시각(여유값 없이)
        CollectionAssert.AreEqual(new[] { "hit0" }, recorder.Order);
        Assert.IsTrue(runner.IsRunning, "타격 뒤에도 행동은 끝까지 진행된다");

        Assert.AreEqual(6.0, runner.NextMarkerTime);
        runner.FireMarkersUpTo(6.0, recorder);
        CollectionAssert.AreEqual(new[] { "hit0", "complete" }, recorder.Order);
        Assert.IsFalse(runner.IsRunning);
        Assert.AreEqual(double.PositiveInfinity, runner.NextMarkerTime);
    }

    [Test]
    public void JustBeforeAMarker_DoesNotFireIt()
    {
        var runner = new ActionRunner();
        var recorder = new Recorder();
        runner.Start(Attack(), 1.0, 0.0);

        runner.FireMarkersUpTo(runner.NextMarkerTime - 1e-12, recorder);
        Assert.IsEmpty(recorder.Order);

        runner.FireMarkersUpTo(runner.NextMarkerTime, recorder);
        runner.FireMarkersUpTo(runner.EndTime - 1e-12, recorder);
        CollectionAssert.AreEqual(new[] { "hit0" }, recorder.Order, "끝 직전에는 완료되지 않는다");
        Assert.IsTrue(runner.IsRunning);
    }

    [Test]
    public void OneLongAdvance_FiresAllMarkersInOrder()
    {
        var runner = new ActionRunner();
        var recorder = new Recorder();
        runner.Start(Attack(), 1.0, 0.0);

        runner.FireMarkersUpTo(100.0, recorder);

        CollectionAssert.AreEqual(new[] { "hit0", "complete" }, recorder.Order);
    }

    [Test]
    public void HitAtEnd_FiresHitThenComplete_AtExactlyTheEndTime()
    {
        ActionTiming hitAtEnd = ActionTiming.FromValues("Attack", 1f, new[] { 1f });
        var runner = new ActionRunner();
        var recorder = new Recorder();
        runner.Start(hitAtEnd, 0.3, 2.1);

        Assert.AreEqual(runner.EndTime, runner.NextMarkerTime, 0.0, "비율 1의 타격은 끝 시각과 정확히 같다");
        runner.FireMarkersUpTo(runner.EndTime, recorder);

        CollectionAssert.AreEqual(new[] { "hit0", "complete" }, recorder.Order);
    }

    [Test]
    public void MultipleHits_EachFireOnce()
    {
        ActionTiming triple = ActionTiming.FromValues("Skill", 1f, new[] { 0.2f, 0.5f, 0.5f });
        var runner = new ActionRunner();
        var recorder = new Recorder();
        runner.Start(triple, 0.1, 0.0);

        for (int i = 1; i <= 40; i++) runner.FireMarkersUpTo(i * 0.003, recorder);

        CollectionAssert.AreEqual(new[] { "hit0", "hit1", "hit2", "complete" }, recorder.Order);
    }

    [Test]
    public void NullListener_StillAdvancesThroughAllMarkers()
    {
        // listener?.M(i++)처럼 증가를 인자 안에 두면 null일 때 증가가 건너뛰어져 무한 반복이 된다(실제로 겪은 버그).
        var runner = new ActionRunner();
        runner.Start(ActionTiming.FromValues("Skill", 1f, new[] { 0.2f, 0.5f }), 1.0, 0.0);

        runner.FireMarkersUpTo(0.3, null);
        Assert.AreEqual(0.5, runner.NextMarkerTime, 1e-9);

        runner.FireMarkersUpTo(1.0, null);
        Assert.IsFalse(runner.IsRunning);
    }

    [TestCase(1.0)]
    [TestCase(0.013)]
    [TestCase(0.005)]
    public void ChainedActions_StartExactlyAtPreviousEnd(double duration)
    {
        // 다음 행동은 앞 행동의 끝 시각(저장된 값) 그대로에서 시작한다. 1만 번 이어도 시간을 잃거나 더하지 않는다.
        ActionTiming attack = Attack();
        var runner = new ActionRunner();
        double start = 0.0;
        for (int i = 0; i < 10000; i++)
        {
            runner.Start(attack, duration, start);
            double end = runner.EndTime;
            runner.FireMarkersUpTo(end, null);
            Assert.IsFalse(runner.IsRunning);
            start = end;
        }

        Assert.AreEqual(10000 * duration, start, 1e-9);
    }

    [TestCase(0.0)]
    [TestCase(-1.0)]
    [TestCase(double.NaN)]
    [TestCase(double.PositiveInfinity)]
    public void Start_RejectsInvalidDuration(double duration)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActionRunner().Start(Attack(), duration, 0.0));
    }

    [Test]
    public void Start_RejectsInvalidStartTime()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActionRunner().Start(Attack(), 1.0, double.NaN));
        Assert.Throws<ArgumentOutOfRangeException>(() => new ActionRunner().Start(Attack(), 1.0, double.PositiveInfinity));
    }

    [Test]
    public void StartDuringFiring_Throws()
    {
        var runner = new ActionRunner();
        var recorder = new Recorder();
        recorder.OnHitCallback = () => runner.Start(Attack(), 1.0, 0.5);
        runner.Start(Attack(), 1.0, 0.0);

        Assert.Throws<InvalidOperationException>(() => runner.FireMarkersUpTo(1.0, recorder));
    }

    [Test]
    public void Cancel_ClearsAction()
    {
        var runner = new ActionRunner();
        runner.Start(Attack(), 1.0, 0.0);
        runner.Cancel();

        Assert.IsFalse(runner.IsRunning);
        Assert.AreEqual(double.PositiveInfinity, runner.NextMarkerTime);
    }

    [TestCase(0f, new[] { 0.5f })]          // 길이 0
    [TestCase(float.PositiveInfinity, new[] { 0.5f })]
    [TestCase(1f, new[] { 1.1f })]          // 타격이 끝보다 뒤
    [TestCase(1f, new[] { 0.6f, 0.3f })]    // 타격 순서 뒤집힘
    [TestCase(1f, new[] { 0f })]            // 타격 0
    [TestCase(1f, new[] { float.NaN })]
    [TestCase(1f, new float[0])]            // 공격인데 타격 없음
    public void FromValues_RejectsBrokenTimeline(float clipLength, float[] hits)
    {
        Assert.Throws<ArgumentException>(() => ActionTiming.FromValues("Attack", clipLength, hits));
    }

    [Test]
    public void FromValues_DeathMustHaveNoHits()
    {
        Assert.Throws<ArgumentException>(() => ActionTiming.FromValues("Death", 1f, new[] { 0.5f }));
        Assert.DoesNotThrow(() => ActionTiming.FromValues("Death", 1f, new float[0]));
    }
}
