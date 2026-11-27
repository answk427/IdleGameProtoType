using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

// CombatScheduler + CombatActions(docs/COMBAT_ARCHITECTURE.md 7-3, 7-11, 7-13).
// 사건은 단계 안의 정확한 시각에, 모든 전투원 사이의 시간 순서대로 처리된다.
// 취소 없음: 모든 행동은 끝까지 하고, 다음 행동은 앞 행동이 완료된 시각에 시작한다.
// 기대 시각은 러너와 같은 방식(시작 + 비율 × 길이, 다음 시작 = 앞 끝)으로 계산해 정확히 같은지(오차 0) 비교한다.
[TestFixture]
public class CombatSchedulerTests
{
    private const double Step = 0.02;

    private sealed class StartRecord
    {
        public string Key;
        public double Time;
        public double Duration;
        public Skill Skill;
        public int Hits;
    }

    private sealed class FakeActor : ICombatActor
    {
        public readonly SimClock Clock;
        public readonly CombatActions Combat;
        public readonly Dictionary<string, ActionTiming> Timelines = new Dictionary<string, ActionTiming>();
        public readonly List<Skill> Skills = new List<Skill>();
        public double AttackDuration = 1.0;
        public bool BasicAttack = true;
        public bool Alive = true;
        public bool Target = true;
        public FakeActor Opponent;
        public bool KillsOpponentOnHit;

        public readonly List<double> BasicHitTimes = new List<double>();
        public readonly List<double> SkillHitTimes = new List<double>();
        public readonly List<StartRecord> Starts = new List<StartRecord>();
        public int IdleCalls;

        public FakeActor(SimClock clock)
        {
            Clock = clock;
            Combat = new CombatActions(this);
            Timelines["Attack"] = AttackTimeline();
            Timelines["Skill"] = SkillTimeline();
            Combat.Engage();
        }

        public bool IsAlive => Alive;
        public bool TryGetTiming(string key, out ActionTiming timing) => Timelines.TryGetValue(key, out timing);
        public bool HasBasicAttack => BasicAttack;
        public double BasicAttackDuration => AttackDuration;
        public bool TryAcquireTarget() => Alive && Target && (Opponent == null || Opponent.Alive);

        public Skill PickReadySkill(double now)
        {
            foreach (Skill skill in Skills)
            {
                if (skill.IsReadyAt(now)) return skill;
            }
            return null;
        }

        public double NextDecisionInputChange(double now)
        {
            double next = double.PositiveInfinity;
            foreach (Skill skill in Skills)
            {
                if (skill.ReadyAt > now) next = Math.Min(next, skill.ReadyAt);
            }
            return next;
        }

        public void ApplyBasicAttackHit()
        {
            BasicHitTimes.Add(Clock.Now);
            CountHit();
            if (KillsOpponentOnHit && Opponent != null) Opponent.Die();
        }

        public void ApplySkillHit(Skill skill)
        {
            SkillHitTimes.Add(Clock.Now);
            CountHit();
        }

        public void OnActionStarted(ActionTiming action, double duration, Skill skill) =>
            Starts.Add(new StartRecord { Key = action.Key, Time = Clock.Now, Duration = duration, Skill = skill });

        public void OnActionsIdle() => IdleCalls++;

        // 실제 게임에서는 사망 → 상태 전이 → 교전 종료(Disengage)다.
        public void Die()
        {
            Alive = false;
            Combat.Disengage();
        }

        private void CountHit()
        {
            if (Starts.Count > 0) Starts[Starts.Count - 1].Hits++;
        }

        public List<StartRecord> SkillStarts() => Starts.FindAll(s => s.Skill != null);
    }

    // 기본 공격: 1.2초 클립, 타격 0.7초(지금 플레이어 클립과 같은 비율)
    private static ActionTiming AttackTimeline() => ActionTiming.FromValues("Attack", 1.2f, new[] { 0.7f / 1.2f });

    // 스킬: 0.5초 클립, 타격 0.2초(길이는 클립이 아니라 스킬의 castTime이 정한다)
    private static ActionTiming SkillTimeline() => ActionTiming.FromValues("Skill", 0.5f, new[] { 0.4f });

    private static void Run(SimClock clock, CombatScheduler scheduler, int steps, params FakeActor[] actors) =>
        Run(clock, scheduler, steps, null, actors);

    private static void Run(SimClock clock, CombatScheduler scheduler, int steps, Action<int> beforeStep, params FakeActor[] actors)
    {
        var participants = new List<CombatActions>();
        for (int s = 0; s < steps; s++)
        {
            beforeStep?.Invoke(s);
            participants.Clear();
            foreach (FakeActor actor in actors)
            {
                if (actor.Combat.IsActive) participants.Add(actor.Combat);
            }
            scheduler.RunStep(clock, participants);
            clock.AdvanceStep();
        }
    }

    // 러너와 같은 덧셈으로, start부터 duration씩 이어지는 행동 중 처음으로 at 이후(포함)에 끝나는 행동의 끝 시각.
    private static double FirstCompletionAtOrAfter(double start, double duration, double at)
    {
        double t = start;
        while (t < at) t += duration;
        return t;
    }

    [TestCase(1.0)]
    [TestCase(0.42)]
    [TestCase(0.1)]
    [TestCase(0.013)] // 단계보다 짧음
    [TestCase(0.005)] // 한 단계에 4번
    public void BasicAttacks_HitAtExactTimes_ForAnyInterval(double interval)
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock) { AttackDuration = interval };
        const int steps = 1500;

        Run(clock, new CombatScheduler(), steps, actor);

        // 러너와 같은 계산으로 기대 시각을 만든다: k번째 공격은 앞 공격의 끝에서 시작, 타격은 시작 + 비율 × 길이
        double end = steps * Step;
        float hitFraction = actor.Timelines["Attack"].HitFractions[0];
        var expected = new List<double>();
        for (double start = 0.0; start + hitFraction * interval <= end; start += interval)
        {
            expected.Add(start + hitFraction * interval);
        }

        Assert.AreEqual(expected.Count, actor.BasicHitTimes.Count, "타격 수");
        for (int i = 0; i < expected.Count; i++)
        {
            Assert.AreEqual(expected[i], actor.BasicHitTimes[i], 0.0, $"{i}번째 타격 시각(오차 0)");
        }
    }

    [Test]
    public void NextBasicAttack_StartsExactlyAtPreviousCompletion()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);

        Run(clock, new CombatScheduler(), 150, actor);

        Assert.AreEqual(0.0, actor.Starts[0].Time);
        Assert.AreEqual(1.0, actor.Starts[1].Time, 0.0);
        Assert.AreEqual(2.0, actor.Starts[2].Time, 0.0);
    }

    [Test]
    public void SkillReadyMidAttack_DoesNotCancelIt_AndStartsExactlyAtItsCompletion()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        Skill skill = TestSkills.Create(cooldown: 0.5f, castTime: 0.75f);
        skill.BeginCooldown(0.0); // 0.5초에 준비 → 첫 공격(0~1초) 도중
        actor.Skills.Add(skill);

        Run(clock, new CombatScheduler(), 100, actor);

        Assert.AreEqual("Attack", actor.Starts[0].Key);
        Assert.AreEqual(1, actor.Starts[0].Hits, "기본 공격은 끝까지 해서 타격이 났다");
        Assert.AreEqual("Skill", actor.Starts[1].Key);
        Assert.AreEqual(1.0, actor.Starts[1].Time, 0.0, "준비된 0.5초가 아니라 공격이 끝난 1.0초");
    }

    [Test]
    public void SkillDuration_IsCastTimeFromData_NotClipLength()
    {
        // 같은 시간표(클립 0.5초)를 쓰는 두 스킬이 각자의 castTime만큼 진행한다.
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        Skill slow = TestSkills.Create(id: 1, cooldown: 100f, castTime: 1.7f);
        Skill fast = TestSkills.Create(id: 2, cooldown: 100f, castTime: 0.03f);
        actor.Skills.Add(slow);
        actor.Skills.Add(fast);

        Run(clock, new CombatScheduler(), 200, actor);

        Assert.AreSame(slow, actor.Starts[0].Skill);
        Assert.AreEqual((double)1.7f, actor.Starts[0].Duration, 0.0);
        Assert.AreSame(fast, actor.Starts[1].Skill);
        Assert.AreEqual(actor.Starts[0].Time + actor.Starts[0].Duration, actor.Starts[1].Time, 0.0, "앞 스킬이 끝난 시각");
        Assert.AreEqual((double)0.03f, actor.Starts[1].Duration, 0.0);
        Assert.AreEqual("Attack", actor.Starts[2].Key);
        Assert.AreEqual(actor.Starts[1].Time + actor.Starts[1].Duration, actor.Starts[2].Time, 0.0);

        float skillHitFraction = actor.Timelines["Skill"].HitFractions[0];
        Assert.AreEqual(actor.Starts[0].Time + skillHitFraction * actor.Starts[0].Duration, actor.SkillHitTimes[0], 0.0,
            "타격은 시전 시간의 비율 지점");
    }

    [Test]
    public void SkillCooldown_StartsAtCast_AndNextCastIsAtFirstCompletionAfterReady()
    {
        // 쿨다운 2초 스킬을 0초에 시전(0.5초). 이어서 기본 공격(0.33초)이 계속되고,
        // 2초에 준비되면 그때 진행 중인 공격이 끝나는 시각에 다시 시전한다.
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock) { AttackDuration = 0.33 };
        Skill skill = TestSkills.Create(cooldown: 2f, castTime: 0.5f);
        actor.Skills.Add(skill);

        Run(clock, new CombatScheduler(), 150, actor);

        List<StartRecord> skillStarts = actor.SkillStarts();
        Assert.AreEqual(0.0, skillStarts[0].Time);
        double readyAt = skillStarts[0].Time + skill.Spec.Cooldown;
        double expectedSecondCast = FirstCompletionAtOrAfter(0.0 + skill.Spec.CastTime, 0.33, readyAt);
        Assert.AreEqual(expectedSecondCast, skillStarts[1].Time, 0.0);
    }

    [Test]
    public void ManualRequest_UsedOnce_AtFirstCompletionAfterReady()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        Skill manual = TestSkills.Create(id: 7, cooldown: 2.95f);
        manual.BeginCooldown(0.0);
        double readyAt = manual.ReadyAt;
        actor.Combat.RequestSkill(manual); // 시전자의 스킬 목록에 없는 수동 요청

        Run(clock, new CombatScheduler(), 250, actor);

        List<StartRecord> skillStarts = actor.SkillStarts();
        Assert.AreEqual(1, skillStarts.Count, "요청은 한 번만 쓰인다");
        Assert.AreEqual(FirstCompletionAtOrAfter(0.0, 1.0, readyAt), skillStarts[0].Time, 0.0, "2.95초가 아니라 그때 진행 중인 공격이 끝난 3.0초");
    }

    [Test]
    public void ManualRequest_TakesPriorityOverReadySkill()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        Skill auto = TestSkills.Create(id: 1, cooldown: 100f);
        Skill manual = TestSkills.Create(id: 7, cooldown: 100f);
        actor.Skills.Add(auto);
        actor.Combat.RequestSkill(manual);

        Run(clock, new CombatScheduler(), 100, actor);

        Assert.AreSame(manual, actor.Starts[0].Skill);
        Assert.AreSame(auto, actor.Starts[1].Skill);
    }

    [Test]
    public void SkillOnlyActor_CastsExactlyAtReadyTime()
    {
        // 기본 공격이 없는 시전자(데이터: 공격 간격 0)는 행동 없이 기다리다가, 스킬이 준비되는 시각(결정 입력이 바뀌는 사건)에
        // 정확히 시전한다. 단계 경계(2.96초)로 밀리지 않고, 기본 공격이 없다는 것은 에러가 아니다.
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock) { BasicAttack = false };
        Skill skill = TestSkills.Create(cooldown: 2.95f, castTime: 0.5f);
        skill.BeginCooldown(0.0);
        double firstReady = skill.ReadyAt;
        actor.Skills.Add(skill);

        Run(clock, new CombatScheduler(), 400, actor);
        LogAssert.NoUnexpectedReceived();

        Assert.IsTrue(actor.Starts.TrueForAll(s => s.Skill != null), "기본 공격을 시작하지 않는다");
        List<StartRecord> skillStarts = actor.SkillStarts();
        Assert.AreEqual(firstReady, skillStarts[0].Time, 0.0);
        Assert.AreEqual(skillStarts[0].Time + skill.Spec.Cooldown, skillStarts[1].Time, 0.0);
    }

    [TestCase(1.0, 0.001f, 0.5f)]     // 쿨다운이 거의 없는 스킬을 계속 쓴다
    [TestCase(0.005, 0.3f, 0.013f)]   // 단계보다 짧은 공격과 스킬
    [TestCase(0.42, 0.7f, 1.0f)]      // 스킬이 공격보다 길다
    [TestCase(0.013, 0.05f, 0.002f)]  // 극단적으로 짧은 시전
    public void NoCancel_EveryActionCompletesWithAllHits_AndNextStartsExactlyAtItsEnd(double attack, float cooldown, float castTime)
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock) { AttackDuration = attack };
        actor.Timelines["Skill"] = ActionTiming.FromValues("Skill", 0.5f, new[] { 0.3f, 0.6f, 1f }); // 끝 시각의 타격 포함
        Skill a = TestSkills.Create(id: 1, cooldown: cooldown, castTime: castTime);
        Skill b = TestSkills.Create(id: 2, cooldown: cooldown * 3f, castTime: castTime * 2f);
        Skill manual = TestSkills.Create(id: 9, cooldown: cooldown * 5f, castTime: castTime);
        actor.Skills.Add(a);
        actor.Skills.Add(b);

        // 행동 도중에 수동 요청이 계속 들어와도 지금 행동을 끊지 않는다.
        Run(clock, new CombatScheduler(), 500, s => { if (s % 7 == 3) actor.Combat.RequestSkill(manual); }, actor);

        Assert.Greater(actor.Starts.Count, 10);
        for (int i = 0; i + 1 < actor.Starts.Count; i++)
        {
            StartRecord current = actor.Starts[i];
            StartRecord next = actor.Starts[i + 1];
            double expectedDuration = current.Skill != null ? current.Skill.Spec.CastTime : attack;
            int expectedHits = actor.Timelines[current.Key].HitFractions.Count;

            Assert.AreEqual(expectedDuration, current.Duration, 0.0, $"{i}번째 행동 길이는 데이터 값");
            Assert.AreEqual(expectedHits, current.Hits, $"{i}번째 행동({current.Key})의 타격이 모두 났다");
            Assert.AreEqual(current.Time + current.Duration, next.Time, 0.0, $"{i + 1}번째 행동은 앞 행동이 끝난 시각에 시작");
        }

        foreach (Skill skill in new[] { a, b, manual })
        {
            List<StartRecord> casts = actor.Starts.FindAll(s => s.Skill == skill);
            for (int i = 0; i + 1 < casts.Count; i++)
            {
                Assert.GreaterOrEqual(casts[i + 1].Time, casts[i].Time + skill.Spec.Cooldown, $"스킬 {skill.Spec.Id} 쿨다운");
            }
        }
    }

    [TestCase(0f)]
    [TestCase(-1f)]
    public void InvalidCastTime_ReportsOnce_AndFallsBackToBasicAttack(float castTime)
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        actor.Skills.Add(TestSkills.Create(cooldown: 1f, castTime: castTime));

        LogAssert.Expect(LogType.Error, new Regex("시전 시간\\(castTime\\)"));
        Run(clock, new CombatScheduler(), 200, actor);

        Assert.IsTrue(actor.Starts.TrueForAll(s => s.Key == "Attack"));
        Assert.Greater(actor.Starts.Count, 2);
    }

    [Test]
    public void EventsOfDifferentActors_AreProcessedInTimeOrder_WithinAStep()
    {
        // 같은 단계(0~0.02초) 안: B가 0.005초에 A를 죽이고, A는 0.018초에 때릴 예정이었다.
        // A가 목록에서 먼저여도 시간 순서로 처리되므로 A의 타격은 일어나지 않는다.
        var clock = new SimClock(Step);
        var a = new FakeActor(clock) { AttackDuration = 0.02 };
        var b = new FakeActor(clock) { AttackDuration = 0.02, KillsOpponentOnHit = true };
        a.Timelines["Attack"] = ActionTiming.FromValues("Attack", 1f, new[] { 0.9f });  // 0.018초
        b.Timelines["Attack"] = ActionTiming.FromValues("Attack", 1f, new[] { 0.25f }); // 0.005초
        a.Opponent = b;
        b.Opponent = a;

        Run(clock, new CombatScheduler(), 1, a, b);

        Assert.AreEqual(1, b.BasicHitTimes.Count);
        Assert.AreEqual(0, a.BasicHitTimes.Count, "먼저 죽은 쪽의 뒤 사건은 처리되지 않는다");
        Assert.IsFalse(a.Alive);
    }

    [Test]
    public void SimultaneousEvents_FollowParticipantOrder()
    {
        // 둘 다 정확히 0.01초에 때리고 서로를 죽인다. 같은 시각이면 목록 순서(플레이어 → 몬스터 생성 순)로 처리한다.
        var clock = new SimClock(Step);
        var first = new FakeActor(clock) { AttackDuration = 0.02, KillsOpponentOnHit = true };
        var second = new FakeActor(clock) { AttackDuration = 0.02, KillsOpponentOnHit = true };
        first.Timelines["Attack"] = ActionTiming.FromValues("Attack", 1f, new[] { 0.5f });
        second.Timelines["Attack"] = ActionTiming.FromValues("Attack", 1f, new[] { 0.5f });
        first.Opponent = second;
        second.Opponent = first;

        Run(clock, new CombatScheduler(), 1, first, second);

        Assert.AreEqual(1, first.BasicHitTimes.Count);
        Assert.AreEqual(0, second.BasicHitTimes.Count);
    }

    [Test]
    public void NoTarget_StartsNothing_AndBecomesIdleAfterCurrentAction()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        var scheduler = new CombatScheduler();
        Run(clock, scheduler, 10, actor); // 공격 시작
        actor.Target = false;

        Run(clock, scheduler, 100, actor);

        Assert.IsTrue(actor.Combat.IsFree);
        Assert.AreEqual(1, actor.Starts.Count);
        Assert.AreEqual(1, actor.Starts[0].Hits, "대상이 없어져도 시작한 공격은 끝까지 한다");
        Assert.AreEqual(1, actor.IdleCalls);
    }

    [Test]
    public void MissingTimeline_ReportsOnce_AndDoesNotThrow()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        actor.Timelines.Remove("Attack");

        LogAssert.Expect(LogType.Error, new Regex("'Attack' 행동 시간표가 없습니다"));
        Run(clock, new CombatScheduler(), 50, actor);

        Assert.AreEqual(0, actor.Starts.Count);
    }

    [Test]
    public void TooManyEvents_HitStepCap_AndReport()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock) { AttackDuration = 1e-7 };

        LogAssert.Expect(LogType.Error, new Regex("한 단계의 사건이"));
        Run(clock, new CombatScheduler(), 1, actor);

        Assert.AreEqual(clock.StepStart, clock.Now, "단계는 끝난다");
    }

    [Test]
    public void Disengage_CancelsAction_KeepsRequest()
    {
        var clock = new SimClock(Step);
        var actor = new FakeActor(clock);
        var scheduler = new CombatScheduler();
        Skill manual = TestSkills.Create(cooldown: 1f);
        manual.BeginCooldown(0.0);
        actor.Combat.RequestSkill(manual);
        Run(clock, scheduler, 5, actor);

        actor.Combat.Disengage();
        Assert.IsTrue(actor.Combat.IsFree);

        Run(clock, scheduler, 100, actor); // 교전하지 않으면 아무 일도 없다
        Assert.AreEqual(1, actor.Starts.Count);

        actor.Combat.Engage();
        Run(clock, scheduler, 1, actor);
        Assert.AreSame(manual, actor.Starts[actor.Starts.Count - 1].Skill, "멈춘 뒤에도 수동 요청은 남아 다음 결정에 쓰인다");
    }

    [Test]
    public void Clock_NeverGoesBackward_OrLeavesTheStep()
    {
        var clock = new SimClock(Step);
        clock.SetNow(0.01);

        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetNow(0.005));
        Assert.Throws<ArgumentOutOfRangeException>(() => clock.SetNow(0.03));

        clock.SetNow(clock.StepEnd);
        clock.AdvanceStep();
        Assert.AreEqual(1 * Step, clock.StepStart);
        Assert.AreEqual(clock.StepStart, clock.Now);
    }
}
