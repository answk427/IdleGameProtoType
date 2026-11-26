using System;
using System.Collections.Generic;
using NUnit.Framework;
using Newtonsoft.Json.Linq;
using UnityEngine.TestTools.Constraints;
using static CombatTestKit;
using Is = UnityEngine.TestTools.Constraints.Is;

// 흐름(EncounterDirector, docs/COMBAT_ARCHITECTURE.md 10절)을 씬 없이 돌린다: 진짜 CombatWorld·Combatant + 가짜 호스트.
// 한 단계 = ① 흐름(Tick) → ② ③ ⑤(CombatTestKit.StepOnce). CombatSimulation의 국면 순서와 같다.
// 지금 밸런스 값이 아니라 불변식(정확한 대기, 같은 입력 = 같은 결과, 멈추지 않음, 할당 0)을 본다.
[TestFixture]
public class EncounterDirectorTests
{
    private const int NormalId = 1001;
    private const int BossId = 2001;
    private static readonly FlowTimings DefaultTimings = new FlowTimings(0.5, 1.0, 1.0);

    // ── 도구 ──

    // 몬스터 뷰 대신 전투원만 만든다. 풀처럼 객체를 다시 써서 할당 측정 중에도 할당하지 않는다.
    private sealed class FakeEncounterHost : IEncounterHost
    {
        private readonly List<Combatant> free = new List<Combatant>(64);
        private readonly FakeTimelines timelines = Timelines(attack: true, death: false, skill: false);

        public CombatantStats NormalStats = Stats(hp: 30, policy: MovePolicy.Hold);
        public CombatantStats BossStats = Stats(hp: 100, policy: MovePolicy.Approach, speed: 2f);
        public float HalfWidth = 0.4f;
        public int Spawned, BossesSpawned, Despawned, Released;
        public Action<Combatant, bool, float> OnSpawned;

        public Combatant Spawn(int monsterId, StageData stage, float pivotX, bool isBoss)
        {
            Combatant c;
            if (free.Count > 0)
            {
                c = free[free.Count - 1];
                free.RemoveAt(free.Count - 1);
            }
            else
            {
                c = new Combatant(Team.Enemy);
            }

            c.Spawn(isBoss ? BossStats : NormalStats, pivotX, HalfWidth, timelines, null);
            Spawned++;
            if (isBoss) BossesSpawned++;
            OnSpawned?.Invoke(c, isBoss, pivotX);
            return c;
        }

        public void Despawn(Combatant combatant)
        {
            Despawned++;
            free.Add(combatant);
        }

        public void ReleaseStage(StageData stage) => Released++;
    }

    private sealed class Rig
    {
        public readonly CombatWorld World;
        public readonly FakeEncounterHost Host = new FakeEncounterHost();
        public int EnteredCount;
        public readonly Dictionary<int, StageData> Stages = new Dictionary<int, StageData>();
        public readonly List<(long step, FlowPhase phase)> Phases = new List<(long, FlowPhase)>();
        public readonly List<(long step, StageEntered entry)> Entries = new List<(long, StageEntered)>();
        public readonly List<string> Log = new List<string>();
        public readonly List<(long step, string text)> Stalls = new List<(long, string)>();
        public Combatant Player;
        public EncounterDirector Director;

        private readonly FlowTimings timings;
        private readonly bool record;

        public Rig(FlowTimings timings, bool record = true, params StageData[] stages) : this(CombatTestKit.Step, timings, record, stages) { }

        public Rig(double stepLength, FlowTimings timings, bool record, params StageData[] stages)
        {
            World = new CombatWorld(new SimClock(stepLength));
            this.timings = timings;
            this.record = record;
            foreach (StageData s in stages) Stages[s.StageNumber] = s;
        }

        public long Now => World.Clock.StepIndex;

        public Rig WithPlayer(CombatantStats stats, bool deathTimeline = true)
        {
            Player = Spawn(World, Team.Ally, 0f, 0.4f, stats, Timelines(death: deathTimeline));
            return this;
        }

        public Rig Start(int stage = 1)
        {
            Director = new EncounterDirector(World, Host, n => Stages.TryGetValue(n, out StageData s) ? s : null, timings);
            Director.Stalled.Subscribe(text => Stalls.Add((Now, text)));
            Director.StageEntered.Subscribe(_ => EnteredCount++);
            if (record)
            {
                Director.Phase.Subscribe(p =>
                {
                    Phases.Add((Now, p));
                    Log.Add($"{Now} phase {p}");
                });
                Director.StageEntered.Subscribe(e =>
                {
                    Entries.Add((Now, e));
                    Log.Add($"{Now} enter {e}");
                });
                Host.OnSpawned = (c, boss, x) => Log.Add($"{Now} spawn {(boss ? "boss" : "normal")} {BitConverter.SingleToInt32Bits(x)}");
            }
            Assert.That(Director.Begin(stage), NUnit.Framework.Is.True);
            return this;
        }

        public void Step()
        {
            Director.Tick();
            StepOnce(World);
        }

        public void Run(int steps)
        {
            for (int i = 0; i < steps; i++) Step();
        }

        // 조건이 참이 될 때까지 단계를 돈다(단계 사이에서 확인). 상한을 넘으면 실패한다(무한 반복 방지).
        public void RunUntil(Func<bool> condition, int maxSteps = 200_000, string what = "조건")
        {
            for (int i = 0; i < maxSteps; i++)
            {
                if (condition()) return;
                Step();
            }
            Assert.Fail($"{maxSteps}단계 안에 {what}이(가) 되지 않았습니다. 단계 {Director.Phase.Value}, 스테이지 {Director.CurrentStageNumber.Value}");
        }

        public long StepOf(FlowPhase phase, int occurrence = 0)
        {
            int seen = 0;
            foreach ((long step, FlowPhase p) in Phases)
            {
                if (p != phase) continue;
                if (seen++ == occurrence) return step;
            }
            Assert.Fail($"{phase}가 {occurrence + 1}번째로 기록되지 않았습니다.");
            return -1;
        }

        public long StepOfFirstAfter(FlowPhase phase, long afterStep)
        {
            foreach ((long step, FlowPhase p) in Phases)
                if (p == phase && step >= afterStep) return step;
            Assert.Fail($"{afterStep} 이후의 {phase}가 없습니다.");
            return -1;
        }
    }

    private static StageData Stage(int number, int monsters = 2, int encounters = 2, float spacing = 1.5f,
        float spawnDistance = 3f, double bossTimeLimit = 0.0) =>
        new JObject
        {
            ["stageNumber"] = number,
            ["normalMonsterId"] = NormalId,
            ["bossMonsterId"] = BossId,
            ["monstersPerEncounter"] = monsters,
            ["encountersToComplete"] = encounters,
            ["monsterSpacing"] = spacing,
            ["spawnDistance"] = spawnDistance,
            ["bossTimeLimit"] = bossTimeLimit,
            ["clearType"] = 0,
        }.ToObject<StageData>();

    // 인카운터 몬스터와 보스를 한 방에 잡는 플레이어
    private static CombatantStats StrongPlayer(float speed = 5f) =>
        Stats(hp: 1e6, damage: 1000, interval: 0.1, range: 0.5f, policy: MovePolicy.Advance, speed: speed);

    private static Rig StartedRig(FlowTimings timings, CombatantStats player, bool deathTimeline = true, params StageData[] stages) =>
        new Rig(timings, true, stages).WithPlayer(player, deathTimeline).Start();

    private static void RunUntilBossAvailable(Rig rig) =>
        rig.RunUntil(() => rig.Director.BossChallengeAvailable.Value, what: "보스 도전 가능");

    // ── 한 바퀴 ──

    [Test]
    public void FullCycle_EncountersThenBossThenNextStage_WithExactWaits()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), true, Stage(1, encounters: 2), Stage(2));
        EncounterDirector d = rig.Director;

        rig.Run(1);
        Assert.That(d.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.Encounter), "게임 시작 다음 ①에서 첫 인카운터");
        Assert.That(rig.Host.Spawned, NUnit.Framework.Is.EqualTo(2));
        Assert.That(rig.Host.Released, NUnit.Framework.Is.Zero);

        // 인카운터를 정확히 encountersToComplete번 마친 순간에 도전 가능이 켜진다.
        int clearedWhenAvailable = -1;
        d.BossChallengeAvailable.Subscribe(on => { if (on) clearedWhenAvailable = d.EncountersCleared; });
        RunUntilBossAvailable(rig);
        Assert.That(clearedWhenAvailable, NUnit.Framework.Is.EqualTo(2));

        // 정리 대기는 정확히 0.5초(25단계)
        long cleared = rig.StepOf(FlowPhase.EncounterCleared);
        Assert.That(rig.StepOfFirstAfter(FlowPhase.Encounter, cleared + 1) - cleared, NUnit.Framework.Is.EqualTo(25));

        // 명령: 결과는 지금, 실행은 다음 ①
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.Accepted));
        Assert.That(d.BossChallengeAvailable.Value, NUnit.Framework.Is.False, "받는 순간 버튼이 사라진다");
        Assert.That(d.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.Encounter), "부른 순간에는 실행하지 않는다");
        long requestedAt = rig.Now;
        rig.Run(1);
        Assert.That(d.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.BossIntro));
        Assert.That(rig.StepOf(FlowPhase.BossIntro), NUnit.Framework.Is.EqualTo(requestedAt));
        Assert.That(rig.World.CountAlive(Team.Enemy), NUnit.Framework.Is.Zero, "남은 인카운터 몬스터는 사라진다");

        // 보스 등장 대기는 정확히 1초(50단계)
        long diedAt = -1;
        Combatant bossCombatant = null;
        rig.Host.OnSpawned += (c, boss, x) => { if (boss) bossCombatant = c; };
        // 사건은 단계가 끝나고 시계가 넘어간 뒤 배달된다: 죽은 단계 = 배달 때의 단계 - 1
        rig.World.Events.Subscribe(e => { if (e.Type == CombatEventType.Died && e.Source == bossCombatant) diedAt = rig.Now - 1; });
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Boss, what: "보스전");
        Assert.That(rig.StepOf(FlowPhase.Boss) - rig.StepOf(FlowPhase.BossIntro), NUnit.Framework.Is.EqualTo(50));
        Assert.That(rig.Host.BossesSpawned, NUnit.Framework.Is.EqualTo(1));

        // 처치 → 다음 ①에서 전환 → 가운데(25단계)에서 다음 스테이지 → 끝(50단계)에서 첫 인카운터
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Transition, what: "전환");
        long transition = rig.StepOf(FlowPhase.Transition);
        Assert.That(transition, NUnit.Framework.Is.EqualTo(diedAt + 1), "보스가 죽은 단계 다음 ①");

        rig.RunUntil(() => d.Phase.Value == FlowPhase.Encounter, what: "다음 스테이지 인카운터");
        Assert.That(rig.Entries.Count, NUnit.Framework.Is.EqualTo(2));
        Assert.That(rig.Entries[1].entry, NUnit.Framework.Is.EqualTo(new StageEntered(2, StageEntryReason.Cleared)));
        Assert.That(rig.Entries[1].step - transition, NUnit.Framework.Is.EqualTo(25));
        Assert.That(rig.StepOfFirstAfter(FlowPhase.Encounter, transition) - transition, NUnit.Framework.Is.EqualTo(50));
        Assert.That(d.CurrentStageNumber.Value, NUnit.Framework.Is.EqualTo(2));
        Assert.That(d.EncountersCleared, NUnit.Framework.Is.Zero);
        Assert.That(rig.Host.Released, NUnit.Framework.Is.EqualTo(1), "다른 스테이지로 가면 이전 스테이지 자원을 비운다");
    }

    [Test]
    public void Begin_EntersWithoutTransition_AndReportsStart()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), true, Stage(1));

        Assert.That(rig.Director.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.Starting));
        Assert.That(rig.Director.CurrentStageNumber.Value, NUnit.Framework.Is.EqualTo(1));
        Assert.That(rig.Entries.Count, NUnit.Framework.Is.EqualTo(1));
        Assert.That(rig.Entries[0].entry.Reason, NUnit.Framework.Is.EqualTo(StageEntryReason.Start));
        Assert.That(rig.Host.Spawned, NUnit.Framework.Is.Zero, "스폰은 다음 ①에서");
    }

    [Test]
    public void LastStage_Cleared_RestartsSameStage_AndKeepsItsResources()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), true, Stage(1, encounters: 1));
        EncounterDirector d = rig.Director;

        RunUntilBossAvailable(rig);
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.Accepted));
        rig.RunUntil(() => rig.Entries.Count == 2, what: "클리어 뒤 입장");

        Assert.That(rig.Entries[1].entry, NUnit.Framework.Is.EqualTo(new StageEntered(1, StageEntryReason.Cleared)));
        Assert.That(rig.Host.Released, NUnit.Framework.Is.Zero);
    }

    [Test]
    public void BossDeathAndAllyWipeInSameStep_ClearWins()
    {
        // 사망 시간표가 없는 아군은 사망과 같은 단계에 사망 행동을 마친다.
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), false, Stage(1, encounters: 1), Stage(2));
        rig.Host.BossStats = Stats(hp: 1e12, policy: MovePolicy.Hold);
        EncounterDirector d = rig.Director;

        RunUntilBossAvailable(rig);
        d.RequestBossChallenge();
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Boss, what: "보스전");

        // 두 결과가 같은 단계에 생기게 한다: 둘 다 죽이고 디렉터 없이 한 단계(③에서 아군의 사망 행동이 끝난다).
        d.Boss.TakeDamage(d.Boss.MaxHp);
        rig.Player.TakeDamage(rig.Player.MaxHp);
        StepOnce(rig.World);
        Assert.That(rig.World.IsDefeated(Team.Ally), NUnit.Framework.Is.True);
        Assert.That(d.Boss.IsAlive, NUnit.Framework.Is.False);

        rig.RunUntil(() => rig.Entries.Count == 2, what: "입장");
        Assert.That(rig.Entries[1].entry, NUnit.Framework.Is.EqualTo(new StageEntered(2, StageEntryReason.Cleared)));
    }

    // ── 전멸 ──

    [TestCase(FlowPhase.Encounter)]
    [TestCase(FlowPhase.EncounterCleared)]
    [TestCase(FlowPhase.BossIntro)]
    [TestCase(FlowPhase.Boss)]
    public void AllyWipe_InAnyPhase_RestartsSameStage(FlowPhase phase)
    {
        // 그 단계에 머물게 대기를 길게, 적은 죽지 않게 둔다.
        var timings = new FlowTimings(phase == FlowPhase.EncounterCleared ? 100 : 0.5, phase == FlowPhase.BossIntro ? 100 : 1.0, 1.0);
        Rig rig = StartedRig(timings, StrongPlayer(), true, Stage(1, encounters: 1), Stage(2));
        EncounterDirector d = rig.Director;
        if (phase == FlowPhase.Encounter) rig.Host.NormalStats = Stats(hp: 1e12, policy: MovePolicy.Hold);
        rig.Host.BossStats = Stats(hp: 1e12, policy: MovePolicy.Hold);

        if (phase == FlowPhase.BossIntro || phase == FlowPhase.Boss)
        {
            RunUntilBossAvailable(rig);
            d.RequestBossChallenge();
        }
        rig.RunUntil(() => d.Phase.Value == phase, what: phase.ToString());

        rig.Player.TakeDamage(rig.Player.MaxHp);
        rig.Run(1);
        Assert.That(d.Phase.Value, NUnit.Framework.Is.EqualTo(phase), "사망 행동(1초)을 마칠 때까지는 전멸이 아니다");

        rig.RunUntil(() => rig.Entries.Count == 2, what: "전멸 뒤 입장");
        Assert.That(rig.Entries[1].entry, NUnit.Framework.Is.EqualTo(new StageEntered(1, StageEntryReason.Defeated)));
        Assert.That(rig.Player.IsAlive, NUnit.Framework.Is.True, "입장할 때 부활");
        Assert.That(rig.Player.Hp, NUnit.Framework.Is.EqualTo(rig.Player.MaxHp));
        Assert.That(d.BossChallengeAvailable.Value, NUnit.Framework.Is.False);
        Assert.That(d.EncountersCleared, NUnit.Framework.Is.Zero);
    }

    // 회귀: 예전에는 도전 가능 상태에서 죽으면 버튼이 남고, 누르면 보스전이 시작됐다.
    [Test]
    public void BossChallengeAvailable_ThenWipe_TurnsOffAndRejectsRequest()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), true, Stage(1, encounters: 1));
        EncounterDirector d = rig.Director;
        RunUntilBossAvailable(rig);

        rig.Player.TakeDamage(rig.Player.MaxHp);
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.Busy), "죽어 있는 동안은 받지 않는다");

        rig.RunUntil(() => d.Phase.Value == FlowPhase.Transition, what: "전환");
        Assert.That(d.BossChallengeAvailable.Value, NUnit.Framework.Is.False, "전환이 시작되면 꺼진다");
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.NotAvailable));

        rig.RunUntil(() => d.Phase.Value == FlowPhase.Encounter, what: "다시 인카운터");
        Assert.That(d.BossChallengeAvailable.Value, NUnit.Framework.Is.False);
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.NotAvailable));
        Assert.That(rig.Host.BossesSpawned, NUnit.Framework.Is.Zero);
    }

    // 받은 뒤 실행 전(같은 단계)에 전멸이 보이면 보스 도전은 버린다.
    [Test]
    public void AcceptedBossChallenge_DroppedWhenWipeComesFirst()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), false, Stage(1, encounters: 1));
        EncounterDirector d = rig.Director;
        RunUntilBossAvailable(rig);

        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.Accepted));
        rig.Player.TakeDamage(rig.Player.MaxHp); // 사망 시간표 없음: 이번 단계 ③에서 사망 행동까지 끝난다
        StepOnce(rig.World);                     // 디렉터 없이 한 단계(받은 명령은 아직 실행 전)
        rig.Run(1);

        Assert.That(d.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.Transition));
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Encounter, what: "다시 인카운터");
        Assert.That(rig.Host.BossesSpawned, NUnit.Framework.Is.Zero);
        Assert.That(rig.Entries[1].entry.Reason, NUnit.Framework.Is.EqualTo(StageEntryReason.Defeated));
    }

    // ── 명령 결과 ──

    [Test]
    public void Commands_ReturnResults_AndRunOnlyAtNextTick()
    {
        var rig = new Rig(DefaultTimings, true, Stage(1, encounters: 1), Stage(2)).WithPlayer(StrongPlayer());
        var director = new EncounterDirector(rig.World, rig.Host, n => rig.Stages.TryGetValue(n, out StageData s) ? s : null, DefaultTimings);
        Assert.That(director.RestartAt(1), NUnit.Framework.Is.EqualTo(RestartResult.Busy), "시작 전");
        Assert.That(director.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.NotAvailable));

        rig.Start();
        EncounterDirector d = rig.Director;
        Assert.That(d.RestartAt(99), NUnit.Framework.Is.EqualTo(RestartResult.NoSuchStage));
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.NotAvailable));

        RunUntilBossAvailable(rig);
        Assert.That(d.RestartAt(2), NUnit.Framework.Is.EqualTo(RestartResult.Accepted));
        Assert.That(d.RestartAt(2), NUnit.Framework.Is.EqualTo(RestartResult.Busy), "이미 받은 재시작");
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.Busy), "재시작을 받아 둠");
        Assert.That(d.Phase.Value, NUnit.Framework.Is.Not.EqualTo(FlowPhase.Transition), "부른 순간에는 실행하지 않는다");

        rig.Run(1);
        Assert.That(d.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.Transition));
        Assert.That(d.RestartAt(1), NUnit.Framework.Is.EqualTo(RestartResult.Busy), "전환 중");

        rig.RunUntil(() => d.Phase.Value == FlowPhase.Encounter, what: "인카운터");
        Assert.That(rig.Entries[1].entry, NUnit.Framework.Is.EqualTo(new StageEntered(2, StageEntryReason.Requested)));

        RunUntilBossAvailable(rig);
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.Accepted));
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.NotAvailable), "이미 받음");
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Boss, what: "보스전");
        Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.NotAvailable), "보스전 중");
        Assert.That(d.RestartAt(2), NUnit.Framework.Is.EqualTo(RestartResult.Accepted), "보스전 중에도 다시 시작할 수 있다");
    }

    // 사건을 받는 쪽이 명령을 불러도 그 사건 안에서는 실행되지 않는다(재진입 없음).
    [Test]
    public void CommandFromEventHandler_RunsAtNextTick()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), true, Stage(1, encounters: 1));
        EncounterDirector d = rig.Director;
        long requestedAt = -1;
        d.BossChallengeAvailable.Subscribe(on =>
        {
            if (!on || requestedAt >= 0) return;
            requestedAt = rig.Now;
            Assert.That(d.RequestBossChallenge(), NUnit.Framework.Is.EqualTo(BossChallengeResult.Accepted));
            Assert.That(d.Phase.Value, NUnit.Framework.Is.Not.EqualTo(FlowPhase.BossIntro));
        });

        rig.RunUntil(() => d.Phase.Value == FlowPhase.BossIntro, what: "보스 등장 대기");
        Assert.That(rig.StepOf(FlowPhase.BossIntro), NUnit.Framework.Is.EqualTo(requestedAt + 1));
    }

    // ── 시간 ──

    // (대기, 단계 수, 전환 가운데 단계 수). 단계 길이 0.02초. 대기는 "n × 단계 ≥ 대기"인 가장 작은 n이고, 0이면 없다.
    [TestCase(0.0, 0, 0)]
    [TestCase(0.02, 1, 1)]
    [TestCase(0.03, 2, 1)]
    [TestCase(0.5, 25, 13)]
    [TestCase(1.0, 50, 25)]
    [TestCase(3.7, 185, 93)]
    public void Waits_EndExactlyAtDataValue(double wait, int steps, int middleSteps)
    {
        // 정리 대기
        Rig rig = StartedRig(new FlowTimings(wait, 1.0, 1.0), StrongPlayer(), true, Stage(1, encounters: 9));
        rig.RunUntil(() => rig.Host.Spawned >= 4, what: "두 번째 인카운터");
        long cleared = rig.StepOf(FlowPhase.EncounterCleared);
        Assert.That(rig.StepOfFirstAfter(FlowPhase.Encounter, cleared) - cleared, NUnit.Framework.Is.EqualTo(steps), "정리 대기");

        // 보스 등장 대기
        rig = StartedRig(new FlowTimings(0.5, wait, 1.0), StrongPlayer(), true, Stage(1, encounters: 1));
        RunUntilBossAvailable(rig);
        rig.Director.RequestBossChallenge();
        rig.RunUntil(() => rig.Director.Phase.Value == FlowPhase.Boss, what: "보스전");
        Assert.That(rig.StepOf(FlowPhase.Boss) - rig.StepOf(FlowPhase.BossIntro), NUnit.Framework.Is.EqualTo(steps), "보스 등장 대기");

        // 전환
        rig = StartedRig(new FlowTimings(0.5, 1.0, wait), StrongPlayer(), true, Stage(1), Stage(2));
        rig.Run(3);
        Assert.That(rig.Director.RestartAt(2), NUnit.Framework.Is.EqualTo(RestartResult.Accepted));
        rig.RunUntil(() => rig.Entries.Count == 2 && rig.Director.Phase.Value == FlowPhase.Encounter, what: "전환 끝");
        long transition = rig.StepOf(FlowPhase.Transition);
        Assert.That(rig.Entries[1].step - transition, NUnit.Framework.Is.EqualTo(middleSteps), "전환 가운데");
        Assert.That(rig.StepOfFirstAfter(FlowPhase.Encounter, transition) - transition, NUnit.Framework.Is.EqualTo(steps), "전환 끝");
    }

    // 게임의 단계 길이는 Unity 설정에서 온다. Unity 6은 설정 0.02를 유리수로 저장해서 fixedDeltaTime(float)이
    // 0.0199999921(비트 1017370374)이다. 그대로 쓰면 0.5초 대기가 26단계가 된다(플레이 측정에서 찾음).
    // 시계는 설정이 뜻하는 0.02를 쓴다.
    [Test]
    public void GameStepLength_FromUnitySetting_KeepsWaitsExact()
    {
        float unityFixedDeltaTime = BitConverter.Int32BitsToSingle(1017370374);
        Assert.That((double)unityFixedDeltaTime, NUnit.Framework.Is.Not.EqualTo(0.02));
        Assert.That(CombatSimulation.StepLengthOf(unityFixedDeltaTime), NUnit.Framework.Is.EqualTo(0.02));
        Assert.That(CombatSimulation.StepLengthOf(0.02f), NUnit.Framework.Is.EqualTo(0.02));
        Assert.That(CombatSimulation.StepLengthOf(1f / 60), NUnit.Framework.Is.EqualTo(0.0166667));

        var rig = new Rig(CombatSimulation.StepLengthOf(unityFixedDeltaTime), new FlowTimings(0.5, 1.0, 1.0), true, Stage(1, encounters: 9))
            .WithPlayer(StrongPlayer()).Start();
        rig.RunUntil(() => rig.Host.Spawned >= 4, what: "두 번째 인카운터");
        long cleared = rig.StepOf(FlowPhase.EncounterCleared);
        Assert.That(rig.StepOfFirstAfter(FlowPhase.Encounter, cleared) - cleared, NUnit.Framework.Is.EqualTo(25));
    }

    [Test]
    public void HugeWait_NeverEnds_WithoutOverflow()
    {
        Rig rig = StartedRig(new FlowTimings(1e300, 1.0, 1.0), StrongPlayer(), true, Stage(1));
        rig.RunUntil(() => rig.Director.Phase.Value == FlowPhase.EncounterCleared, what: "정리 대기");
        rig.Run(5000);
        Assert.That(rig.Director.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.EncounterCleared));
    }

    [Test]
    public void InvalidWaits_AreReportedAndTreatedAsZero()
    {
        UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("encounterClearDelay"));
        UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("bossIntroDelay"));
        UnityEngine.TestTools.LogAssert.Expect(UnityEngine.LogType.Error, new System.Text.RegularExpressions.Regex("transitionDuration"));
        Rig rig = StartedRig(new FlowTimings(-1, double.NaN, double.PositiveInfinity), StrongPlayer(), true, Stage(1));
        rig.RunUntil(() => rig.Host.Spawned >= 4, what: "두 번째 인카운터");
        long cleared = rig.StepOf(FlowPhase.EncounterCleared);
        Assert.That(rig.StepOfFirstAfter(FlowPhase.Encounter, cleared), NUnit.Framework.Is.EqualTo(cleared));
    }

    // ── 보스전 제한 시간 (D25) ──

    [Test]
    public void BossTimeLimit_EndsBossFightExactly_AsDefeat()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), true, Stage(1, encounters: 1, bossTimeLimit: 2.0), Stage(2));
        rig.Host.BossStats = Stats(hp: 1e12, policy: MovePolicy.Hold);
        EncounterDirector d = rig.Director;
        RunUntilBossAvailable(rig);
        d.RequestBossChallenge();
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Boss, what: "보스전");

        Assert.That(d.BossDeadline, NUnit.Framework.Is.EqualTo((rig.StepOf(FlowPhase.Boss) + 100) * Step));
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Transition, what: "제한 시간 초과");
        Assert.That(rig.StepOf(FlowPhase.Transition) - rig.StepOf(FlowPhase.Boss), NUnit.Framework.Is.EqualTo(100));
        Assert.That(d.BossDeadline, NUnit.Framework.Is.EqualTo(double.PositiveInfinity));

        rig.RunUntil(() => rig.Entries.Count == 2, what: "입장");
        Assert.That(rig.Entries[1].entry, NUnit.Framework.Is.EqualTo(new StageEntered(1, StageEntryReason.Defeated)));
    }

    [Test]
    public void BossTimeLimitZero_MeansNoLimit()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(), true, Stage(1, encounters: 1, bossTimeLimit: 0.0));
        rig.Host.BossStats = Stats(hp: 1e15, policy: MovePolicy.Hold);
        EncounterDirector d = rig.Director;
        RunUntilBossAvailable(rig);
        d.RequestBossChallenge();
        rig.RunUntil(() => d.Phase.Value == FlowPhase.Boss, what: "보스전");

        Assert.That(d.BossDeadline, NUnit.Framework.Is.EqualTo(double.PositiveInfinity));
        rig.Run(20_000);
        Assert.That(d.Phase.Value, NUnit.Framework.Is.EqualTo(FlowPhase.Boss));
    }

    // ── 스폰 규칙 ──

    [Test]
    public void Spawn_RecentersOnLead_AndPlacesByStageData()
    {
        Rig rig = StartedRig(DefaultTimings, StrongPlayer(speed: 7.3f), true, Stage(1, monsters: 3, spacing: 1.25f, spawnDistance: 4.5f, encounters: 9));
        var pivots = new List<float>();
        rig.Host.OnSpawned += (c, boss, x) => pivots.Add(x);

        rig.RunUntil(() => rig.Host.Spawned >= 6, what: "두 번째 인카운터");

        // 스폰 직전에 앞장이 원점 근처(0 이상 1 미만)로 오고, 첫 몬스터는 그 앞 spawnDistance, 다음은 monsterSpacing 간격
        float lead = rig.Player.X;
        Assert.That(pivots.Count, NUnit.Framework.Is.EqualTo(6));
        Assert.That(pivots[3] - 4.5f, NUnit.Framework.Is.InRange(0f, 1f));
        Assert.That(pivots[4] - pivots[3], NUnit.Framework.Is.EqualTo(1.25f).Within(1e-4f));
        Assert.That(pivots[5] - pivots[4], NUnit.Framework.Is.EqualTo(1.25f).Within(1e-4f));
        Assert.That(rig.World.TotalShift, NUnit.Framework.Is.GreaterThan(0.0));
        Assert.That(lead, NUnit.Framework.Is.LessThan(pivots[3]));
    }

    // ── 결정성 ──

    [Test]
    public void SameInputs_GiveSameEventLog()
    {
        List<string> RunOnce()
        {
            var rig = new Rig(DefaultTimings, true, Stage(1, encounters: 2), Stage(2, monsters: 3, encounters: 1), Stage(3, monsters: 4, encounters: 1))
                .WithPlayer(Stats(hp: 5000, damage: 120, interval: 0.37, range: 0.8f, policy: MovePolicy.Advance, speed: 3.3f));
            rig.Host.NormalStats = Stats(hp: 200, damage: 7, interval: 0.9, range: 0.3f, policy: MovePolicy.Hold);
            rig.Host.BossStats = Stats(hp: 2500, damage: 60, interval: 1.3, range: 0.4f, policy: MovePolicy.Approach, speed: 1.7f);
            rig.Start();
            rig.Director.BossChallengeAvailable.Subscribe(on => { if (on) rig.Director.RequestBossChallenge(); });

            for (int i = 0; i < 30_000; i++)
            {
                if (i == 7_777) rig.Director.RestartAt(1);
                rig.Step();
            }
            return rig.Log;
        }

        List<string> first = RunOnce();
        List<string> second = RunOnce();
        Assert.That(first.Count, NUnit.Framework.Is.GreaterThan(50), "흐름이 여러 번 돌아야 의미가 있다");
        Assert.That(second, NUnit.Framework.Is.EqualTo(first));
    }

    // ── 진행 보장 (R4) ──

    // 데이터를 무작위로 바꾼 긴 실행: 살아 있는 적이 대열 앞장보다 뒤에 있는 일이 없고, 진전 감시가 경고하지 않는다.
    [Test]
    public void RandomData_LongRuns_NeverStallOrPassThroughEnemies([Range(1, 12)] int seed)
    {
        var random = new Random(seed);
        double R(double min, double max) => min + random.NextDouble() * (max - min);

        var stages = new List<StageData>();
        for (int s = 1; s <= 3; s++)
        {
            stages.Add(Stage(s, monsters: random.Next(1, 7), encounters: random.Next(1, 4), spacing: (float)R(0, 3),
                spawnDistance: (float)R(0.3, 15), bossTimeLimit: random.Next(2) == 0 ? 0.0 : R(5, 60)));
        }

        var timings = new FlowTimings(R(0, 2), R(0, 3), R(0, 3));
        var rig = new Rig(timings, true, stages.ToArray())
            .WithPlayer(Stats(hp: R(500, 5000), damage: R(50, 1000), interval: R(0.05, 0.5), range: (float)R(0, 3),
                policy: MovePolicy.Advance, speed: (float)R(0.5, 30)), deathTimeline: random.Next(2) == 0);
        rig.Host.HalfWidth = (float)R(0.1, 1.5);
        rig.Host.NormalStats = Stats(hp: R(1, 300), damage: R(0, 30), interval: random.Next(2) == 0 ? 0 : R(0.3, 2),
            range: (float)R(0, 2), policy: MovePolicy.Hold);
        rig.Host.BossStats = Stats(hp: R(100, 3000), damage: R(0, 150), interval: R(0.3, 2), range: (float)R(0, 2),
            policy: MovePolicy.Approach, speed: (float)R(0.5, 10));
        rig.Start();
        rig.Director.BossChallengeAvailable.Subscribe(on => { if (on) rig.Director.RequestBossChallenge(); });

        int violations = 0;
        for (int i = 0; i < 9_000; i++)
        {
            rig.Step();
            Combatant lead = rig.World.LeadAlly;
            foreach (Combatant c in rig.World.Combatants)
                if (c.Team == Team.Enemy && c.IsAlive && c.X < lead.X) violations++;
        }

        Assert.That(violations, NUnit.Framework.Is.Zero, "살아 있는 적이 앞장보다 뒤에 있었다");
        Assert.That(rig.Stalls, NUnit.Framework.Is.Empty, rig.Stalls.Count > 0 ? rig.Stalls[0].text : "");
        Assert.That(rig.Phases.Count, NUnit.Framework.Is.GreaterThan(5), "흐름이 움직였다");
    }

    [Test]
    public void ZeroDamage_StallIsReportedOnce_AfterThirtySecondsWithoutProgress()
    {
        Rig rig = StartedRig(DefaultTimings, Stats(hp: 1000, damage: 0, interval: 0.2, range: 0.5f, policy: MovePolicy.Advance, speed: 5f), true,
            Stage(1));
        rig.Host.NormalStats = Stats(hp: 10, policy: MovePolicy.Hold);

        rig.RunUntil(() => rig.Stalls.Count > 0, maxSteps: 10_000, what: "진전 감시 경고");
        long lastProgress = rig.World.LastProgressStep;
        Assert.That(rig.Stalls[0].step - lastProgress, NUnit.Framework.Is.EqualTo(1500), "30초 = 1500단계");
        StringAssert.Contains("진전", rig.Stalls[0].text);

        rig.Run(5000);
        Assert.That(rig.Stalls.Count, NUnit.Framework.Is.EqualTo(1), "진전이 없으면 한 번만 낸다");
    }

    // ── 할당 0 ──

    [Test]
    public void FlowLoop_WithEncountersBossesAndTransitions_AllocatesNothing()
    {
        var rig = new Rig(new FlowTimings(0.1, 0.1, 0.2), false, Stage(1, monsters: 3, encounters: 1), Stage(2, monsters: 2, encounters: 1))
            .WithPlayer(StrongPlayer(speed: 8f));
        rig.Start();
        rig.Director.BossChallengeAvailable.Subscribe(on => { if (on) rig.Director.RequestBossChallenge(); });

        // 첫 실행의 일회성 할당(정적 초기화, JIT, 풀 채우기)을 빼고 잰다.
        rig.Run(3000);
        int enteredBefore = rig.EnteredCount;
        int spawnedBefore = rig.Host.Spawned;

        Assert.That(() => rig.Run(3000), Is.Not.AllocatingGCMemory());
        Assert.That(rig.Host.Spawned - spawnedBefore, NUnit.Framework.Is.GreaterThan(20), "측정 구간에 인카운터가 여러 번 돌았다");
        Assert.That(rig.EnteredCount - enteredBefore, NUnit.Framework.Is.GreaterThan(1), "측정 구간에 스테이지 전환이 있었다");
        Assert.That(rig.Stalls, NUnit.Framework.Is.Empty);
    }
}
