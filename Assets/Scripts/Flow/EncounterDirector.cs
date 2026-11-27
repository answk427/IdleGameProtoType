using System;
using System.Collections.Generic;
using System.Text;

// 흐름의 주인: 인카운터 → 보스 → 스테이지(docs/COMBAT_ARCHITECTURE.md 10절). 순수 C#.
//
// 고정 단계의 첫 국면(①, D22)이다. SimulationLoop가 매 단계 전투 사건(②)보다 먼저 Tick을 부른다.
// ①은 월드 처리 밖이라 스폰·정리, 원점 재설정(D16)을 바로 할 수 있다. 흐름 판단은 여기서만 한다:
// 전투 알림(사망 등)은 흐름을 부르지 않고, 디렉터가 지난 단계의 결과를 월드에 묻는다. 그래서 재진입이 없다.
//
// 상태는 단계 값 하나(FlowPhase). 대기는 끝나는 단계 번호로 저장한다(흐름은 단계 경계에서만 판단하므로,
// 시각 = 단계 번호 × 단계 길이. 7-11의 "절대 시각, 한 번 계산해 저장"과 같은 규칙).
//
// 명령(보스 도전, 스테이지 다시 시작)은 부르는 순간 검사해 결과를 돌려주고, 실행은 다음 ①에서 한다(D23).
// 시뮬레이션이 바뀌는 것은 고정 단계 안뿐이다. 디렉터의 알림을 받는 쪽이 명령을 불러도 그 알림 안에서는 실행되지 않는다.
//
// 한 ①의 순서:
//   1. 받아 둔 재시작 → 전환
//   2. 지난 단계의 결과(전환 중이 아닐 때): 보스 처치 → 클리어, 아군 전멸 → 같은 스테이지, 보스 제한 시간 초과 → 같은 스테이지.
//      같은 단계에 둘이 보이면 위의 것이 먼저다(클리어가 전멸보다 먼저).
//   3. 받아 둔 보스 도전(아직 인카운터 중일 때만)
//   4. 끝난 대기 넘기기. 대기가 0이면 같은 ①에서 다음 단계로 이어진다.
//   5. 진전 감시(에디터·개발 빌드)
//   6. 이 ①의 변화(단계, 스테이지 번호, 보스 도전 가능, 입장, 진전 경고)를 순서대로 바깥에 낸다(D48, 처리 밖)
public sealed class EncounterDirector : IEncounterFlow
{
    // 진전(피해, 사망, 이동, 흐름 변화)이 없는 게임 시간이 이만큼이면 경고한다(R4).
    public const double StallSeconds = 30.0;

    // 한 ①에서 이어지는 단계 변화의 상한. 정상 흐름은 많아야 셋(정리 → 스폰, 전환 가운데 → 끝 → 스폰)이다.
    private const int MaxAdvancesPerTick = 16;

    // 대기의 상한(단계 수). 0.02초 단계로 약 6300년이라 "끝나지 않음"과 같고, 단계 번호에 더해도 넘치지 않는다.
    private const long MaxWaitSteps = 10_000_000_000_000L;

    private readonly CombatWorld world;
    private readonly SimClock clock;
    private readonly IEnemyRoster roster;
    private readonly Func<int, StageSpec> findStage;
    private readonly FlowTimings timings;

    private readonly ReactiveProperty<FlowPhase> phase = new ReactiveProperty<FlowPhase>(FlowPhase.NotStarted);
    private readonly ReactiveProperty<int> currentStageNumber = new ReactiveProperty<int>(0);
    private readonly ReactiveProperty<bool> bossChallengeAvailable = new ReactiveProperty<bool>(false);
    private readonly Subject<StageEntered> stageEntered = new Subject<StageEntered>();
    private readonly Subject<string> stalled = new Subject<string>();

    // 흐름 안의 판단은 이 값들을 읽는다. 바깥에 보이는 값(위의 반응형 값과 사건)은 처리 밖에서만 바뀐다(12절 D48):
    // ① 동안의 변화는 순서대로 모았다가(outbox) ①이 끝난 뒤 낸다. 처리 밖에서 부른 명령(Begin, 보스 도전)의 변화는 그 자리에서 낸다.
    // 그래서 알림을 받는 쪽(저장, 배경, UI)이 디렉터의 동작 한가운데에서 돌지 않는다. 받는 쪽이 보는 순서와 값, 단계 번호는 예전과 같다.
    private FlowPhase phaseNow = FlowPhase.NotStarted;
    private bool bossAvailableNow;
    private readonly List<Notice> outbox = new List<Notice>(16);
    private bool flushing;

    // 이 디렉터가 스폰해서 월드에 등록한 전투원(인카운터 몬스터 또는 보스). 정리할 때 모두 뺀다.
    private readonly List<Combatant> spawned = new List<Combatant>(8);

    private StageSpec stage;
    private Combatant boss;
    private int encountersCleared;

    private long phaseEndStep;          // EncounterCleared, BossIntro, Transition이 끝나는 단계
    private long transitionStartStep;
    private long transitionMiddleStep;  // 스테이지에 들어가는 단계
    private bool transitionEntered;
    private int transitionTarget;
    private StageEntryReason transitionReason;
    private long bossDeadlineStep = long.MaxValue;

    private bool bossRequested;
    private bool restartRequested;
    private int restartTarget;

    private long lastFlowProgressStep;
    private long stallReportedFor = -1; // 경고를 낸 마지막 진전 단계. 그 뒤로 진전이 있으면 다시 감시한다

    public EncounterDirector(CombatWorld world, IEnemyRoster roster, Func<int, StageSpec> findStage, FlowTimings timings)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.roster = roster ?? throw new ArgumentNullException(nameof(roster));
        this.findStage = findStage ?? throw new ArgumentNullException(nameof(findStage));
        clock = world.Clock;
        this.timings = Sanitize(timings);
    }

    // ── 바깥으로 내는 것 (docs/UI_DESIGN.md 5-3의 종류) ──

    // 상태
    public IReadOnlyReactiveProperty<FlowPhase> Phase => phase;
    // 0이면 아직 스테이지에 들어가지 않았다(Begin 전).
    public IReadOnlyReactiveProperty<int> CurrentStageNumber => currentStageNumber;
    // 인카운터를 encountersToComplete번 마치면 켜지고, 도전을 받거나 전환이 시작되면 꺼진다.
    public IReadOnlyReactiveProperty<bool> BossChallengeAvailable => bossChallengeAvailable;

    // 사건
    public Observable<StageEntered> StageEntered => stageEntered;
    // 진전 감시의 경고(에디터·개발 빌드). 값은 상황 요약(단계, 스테이지, 전투원).
    public Observable<string> Stalled => stalled;

    // 매 프레임 값의 원인(끝나는 시각)
    public TransitionTimes Transition => new TransitionTimes(
        transitionStartStep * clock.StepLength, transitionMiddleStep * clock.StepLength, phaseEndStep * clock.StepLength);
    // 보스전 제한 시각. 제한이 없거나 보스전이 아니면 무한대.
    public double BossDeadline =>
        phaseNow == FlowPhase.Boss && bossDeadlineStep != long.MaxValue ? bossDeadlineStep * clock.StepLength : double.PositiveInfinity;

    public SimClock Clock => clock;
    public double Now => clock.Now;
    public StageSpec Stage => stage;
    public int EncountersCleared => encountersCleared;
    public Combatant Boss => boss;

    // ── 시작 ──

    // 전환 없이 스테이지에 들어간다(저장하지 않는다). 다음 ①에서 첫 인카운터를 스폰한다.
    public bool Begin(int stageNumber)
    {
        if (phaseNow != FlowPhase.NotStarted)
        {
            GameLog.Error("[EncounterDirector] 이미 시작했습니다.");
            return false;
        }

        StageSpec first = findStage(stageNumber);
        if (first == null)
        {
            GameLog.Error($"[EncounterDirector] 스테이지 {stageNumber}가 없어 시작하지 않습니다.");
            return false;
        }

        EnterStage(first, StageEntryReason.Start);
        SetPhase(FlowPhase.Starting);
        Flush();
        return true;
    }

    // ── 명령 (D23: 검사와 결과는 지금, 실행은 다음 ①) ──

    public BossChallengeResult RequestBossChallenge()
    {
        if (!bossAvailableNow) return BossChallengeResult.NotAvailable;
        if (bossRequested || restartRequested || !IsInEncounter || !HasLivingAlly) return BossChallengeResult.Busy;

        bossRequested = true;
        SetBossAvailable(false); // 버튼이 바로 사라진다
        Flush();
        return BossChallengeResult.Accepted;
    }

    // 스테이지를 처음부터 시작한다(전환을 거친다). 사망·클리어도 안에서 같은 전환을 쓴다.
    public RestartResult RestartAt(int stageNumber)
    {
        if (phaseNow == FlowPhase.NotStarted || phaseNow == FlowPhase.Transition || restartRequested) return RestartResult.Busy;
        if (findStage(stageNumber) == null) return RestartResult.NoSuchStage;

        restartRequested = true;
        restartTarget = stageNumber;
        return RestartResult.Accepted;
    }

    private bool IsInEncounter => phaseNow == FlowPhase.Encounter || phaseNow == FlowPhase.EncounterCleared;
    // 아군이 모두 죽어 있으면(사망 행동 중 포함) 보스 도전을 받지 않는다. 곧 전멸로 다시 시작한다.
    private bool HasLivingAlly => world.CountAlive(Team.Ally) > 0;

    // ── 고정 단계 ① ──

    public void Tick()
    {
        if (phaseNow == FlowPhase.NotStarted) return;
        long now = clock.StepIndex;

        // 1. 받아 둔 재시작
        if (restartRequested)
        {
            restartRequested = false;
            StartTransition(restartTarget, StageEntryReason.Requested, now);
        }

        // 2. 지난 단계의 결과
        if (phaseNow != FlowPhase.Transition)
        {
            if (phaseNow == FlowPhase.Boss && boss != null && !boss.IsAlive)
                StartTransition(NextStageNumber(), StageEntryReason.Cleared, now);
            else if (world.IsDefeated(Team.Ally))
                StartTransition(stage.StageNumber, StageEntryReason.Defeated, now);
            else if (phaseNow == FlowPhase.Boss && now >= bossDeadlineStep)
                StartTransition(stage.StageNumber, StageEntryReason.Defeated, now);
        }

        // 3. 받아 둔 보스 도전. 그 사이 흐름이 바뀌었거나(전멸 등) 아군이 모두 죽었으면 버린다.
        if (bossRequested)
        {
            bossRequested = false;
            if (IsInEncounter && HasLivingAlly) EnterBossIntro(now);
        }

        // 4. 끝난 대기 넘기기
        for (int i = 0; AdvancePhase(now); i++)
        {
            if (i < MaxAdvancesPerTick) continue;
            GameLog.Error($"[EncounterDirector] 한 단계에서 흐름이 {MaxAdvancesPerTick}번 넘게 바뀌었습니다(지금 {phaseNow}). 다음 단계에서 이어 갑니다.");
            break;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        // 5. 진전 감시
        WatchProgress(now);
#endif

        // 6. 이 ①의 변화를 바깥에 낸다(처리 밖)
        Flush();
    }

    // 지금 단계가 끝났으면 다음 단계로 넘기고 true.
    private bool AdvancePhase(long now)
    {
        switch (phaseNow)
        {
            case FlowPhase.Starting:
                SpawnEncounter();
                SetPhase(FlowPhase.Encounter);
                return true;

            case FlowPhase.Encounter:
                // 끝 = 살아 있는 적이 없다(규칙 하나, 월드에 묻는다). 스폰에 실패했으면(데이터 오류, 에러를 냈다) 끝나지 않고
                // 진전 감시가 드러낸다. 실패를 "깬 것"으로 치면 싸우지 않고 진행돼 버린다.
                if (spawned.Count == 0 || world.CountAlive(Team.Enemy) > 0) return false;
                phaseEndStep = now + StepsFor(timings.EncounterClearDelay);
                SetPhase(FlowPhase.EncounterCleared);
                return true;

            case FlowPhase.EncounterCleared:
                if (now < phaseEndStep) return false;
                DespawnAll();
                encountersCleared++;
                if (encountersCleared >= stage.EncountersToComplete) SetBossAvailable(true);
                SpawnEncounter();
                SetPhase(FlowPhase.Encounter);
                return true;

            case FlowPhase.BossIntro:
                if (now < phaseEndStep) return false;
                SpawnBoss();
                bossDeadlineStep = stage.BossTimeLimit > 0.0 ? now + StepsFor(stage.BossTimeLimit) : long.MaxValue;
                SetPhase(FlowPhase.Boss);
                return true;

            case FlowPhase.Transition:
                if (!transitionEntered)
                {
                    if (now < transitionMiddleStep) return false;
                    transitionEntered = true;
                    EnterStage(FindStageOrCurrent(transitionTarget), transitionReason);
                    lastFlowProgressStep = now;
                    return true;
                }
                if (now < phaseEndStep) return false;
                SpawnEncounter();
                SetPhase(FlowPhase.Encounter);
                return true;

            default:
                return false; // Boss: 끝은 2의 결과가 정한다
        }
    }

    // 보스 도전을 받아 실행: 남은 몬스터는 보상 없이 사라지고(지금 동작), 아군은 완전히 회복한다.
    private void EnterBossIntro(long now)
    {
        DespawnAll();
        for (int i = 0; i < world.Combatants.Count; i++)
        {
            Combatant c = world.Combatants[i];
            if (c.Team == Team.Ally && c.IsAlive) c.Heal(c.MaxHp);
        }
        phaseEndStep = now + StepsFor(timings.BossIntroDelay);
        SetPhase(FlowPhase.BossIntro);
    }

    // 전환 시작. 앞 절반은 전투가 그대로 돌고, 가운데에서 스테이지에 들어간다.
    private void StartTransition(int targetStage, StageEntryReason reason, long now)
    {
        bossRequested = false;
        SetBossAvailable(false);
        bossDeadlineStep = long.MaxValue;

        transitionTarget = targetStage;
        transitionReason = reason;
        transitionEntered = false;
        transitionStartStep = now;
        transitionMiddleStep = now + StepsFor(timings.TransitionDuration * 0.5);
        phaseEndStep = Math.Max(transitionMiddleStep, now + StepsFor(timings.TransitionDuration));
        SetPhase(FlowPhase.Transition);
    }

    // 스테이지에 들어간다: 정리 → (다른 스테이지면) 이전 스테이지 자원 정리 → 진행 초기화 → 아군 부활 → 알림.
    private void EnterStage(StageSpec next, StageEntryReason reason)
    {
        DespawnAll();
        if (stage != null && stage.StageNumber != next.StageNumber) roster.ReleaseStage(stage);

        stage = next;
        if (stage.ClearType != StageClearType.KillBoss)
            GameLog.Error($"[EncounterDirector] 스테이지 {stage.StageNumber}의 클리어 방식 {stage.ClearType}은 처리하지 않습니다. 보스 처치로 진행합니다.");

        encountersCleared = 0;
        SetBossAvailable(false);
        bossDeadlineStep = long.MaxValue;

        if (reason != StageEntryReason.Start)
        {
            for (int i = 0; i < world.Combatants.Count; i++)
            {
                Combatant c = world.Combatants[i];
                if (c.Team == Team.Ally) c.Revive();
            }
        }

        Post(Notice.Stage(stage.StageNumber));
        Post(Notice.StageEnteredNotice(new StageEntered(stage.StageNumber, reason)));
    }

    private int NextStageNumber() => findStage(stage.StageNumber + 1) != null ? stage.StageNumber + 1 : stage.StageNumber;

    // 받을 때 검사한 스테이지가 실행 때 없을 일은 없지만(데이터는 플레이 중에 줄지 않는다), 있으면 지금 스테이지에 머문다.
    private StageSpec FindStageOrCurrent(int stageNumber)
    {
        StageSpec found = findStage(stageNumber);
        if (found != null) return found;

        GameLog.Error($"[EncounterDirector] 스테이지 {stageNumber}가 없어 스테이지 {stage.StageNumber}를 다시 시작합니다.");
        return stage;
    }

    private void SetPhase(FlowPhase next)
    {
        lastFlowProgressStep = clock.StepIndex;
        phaseNow = next;
        Post(Notice.Phase(next));
    }

    private void SetBossAvailable(bool available)
    {
        bossAvailableNow = available;
        Post(Notice.BossAvailable(available));
    }

    // ── 바깥 알림 (처리 밖에서만, D48) ──

    private void Post(in Notice notice) => outbox.Add(notice);

    // 모아 둔 변화를 순서대로 낸다. 받는 쪽이 명령을 불러 새 변화가 생기면 같은 순회에서 이어서 낸다(순서 그대로).
    private void Flush()
    {
        if (flushing) return;
        flushing = true;
        try
        {
            for (int i = 0; i < outbox.Count; i++)
            {
                Notice n = outbox[i];
                switch (n.Kind)
                {
                    case NoticeKind.Phase: phase.Value = (FlowPhase)n.Value; break;
                    case NoticeKind.Stage: currentStageNumber.Value = n.Value; break;
                    case NoticeKind.BossAvailable: bossChallengeAvailable.Value = n.Value != 0; break;
                    case NoticeKind.Entered: stageEntered.OnNext(n.Entered); break;
                    case NoticeKind.Stalled: stalled.OnNext(n.Text); break;
                }
            }
        }
        finally
        {
            outbox.Clear();
            flushing = false;
        }
    }

    private enum NoticeKind : byte { Phase, Stage, BossAvailable, Entered, Stalled }

    // 바깥에 낼 변화 하나(구조체라 모으는 데 할당이 없다).
    private readonly struct Notice
    {
        public readonly NoticeKind Kind;
        public readonly int Value;
        public readonly StageEntered Entered;
        public readonly string Text;

        private Notice(NoticeKind kind, int value, in StageEntered entered = default, string text = null)
        {
            Kind = kind;
            Value = value;
            Entered = entered;
            Text = text;
        }

        public static Notice Phase(FlowPhase phase) => new Notice(NoticeKind.Phase, (int)phase);
        public static Notice Stage(int stageNumber) => new Notice(NoticeKind.Stage, stageNumber);
        public static Notice BossAvailable(bool available) => new Notice(NoticeKind.BossAvailable, available ? 1 : 0);
        public static Notice StageEnteredNotice(in StageEntered entered) => new Notice(NoticeKind.Entered, 0, entered);
        public static Notice Stalled(string text) => new Notice(NoticeKind.Stalled, 0, default, text);
    }

    // ── 스폰 (규칙은 여기, 전투원은 적 풀 IEnemyRoster) ──

    private void SpawnEncounter()
    {
        DespawnAll();
        if (!TryPrepareSpawn(out float firstX)) return;

        for (int i = 0; i < stage.MonstersPerEncounter; i++)
            Register(roster.Spawn(stage.NormalMonsterId, stage, firstX + stage.MonsterSpacing * i, false));

        if (spawned.Count == 0)
            GameLog.Error($"[EncounterDirector] 스테이지 {stage.StageNumber}의 인카운터를 스폰하지 못했습니다(몬스터 {stage.NormalMonsterId}).");
    }

    private void SpawnBoss()
    {
        DespawnAll();
        if (!TryPrepareSpawn(out float x)) return;

        boss = Register(roster.Spawn(stage.BossMonsterId, stage, x, true));
        if (boss == null)
            GameLog.Error($"[EncounterDirector] 스테이지 {stage.StageNumber}의 보스를 스폰하지 못했습니다(몬스터 {stage.BossMonsterId}).");
    }

    private Combatant Register(Combatant combatant)
    {
        if (combatant == null) return null;
        world.Add(combatant);
        spawned.Add(combatant);
        return combatant;
    }

    private void DespawnAll()
    {
        for (int i = spawned.Count - 1; i >= 0; i--)
        {
            Combatant c = spawned[i];
            world.Remove(c);
            roster.Despawn(c);
        }
        spawned.Clear();
        boss = null;
    }

    // 원점을 대열 앞장 근처로 재설정하고(D16), 첫 몬스터의 몸 중심 x = 앞장 몸 중심 + spawnDistance(D18, 9-3, 12절 D50)를 돌려준다.
    // 둘째부터는 monsterSpacing 간격. 화면 크기와 무관한 데이터라 기기 화면비가 전투를 바꾸지 않는다.
    private bool TryPrepareSpawn(out float firstX)
    {
        firstX = 0f;
        Combatant lead = world.LeadAlly;
        if (lead == null)
        {
            GameLog.Error("[EncounterDirector] 대열 앞장(플레이어)이 전투 월드에 없어 스폰 위치를 정할 수 없습니다.");
            return false;
        }

        world.RecenterOn(lead);
        firstX = lead.X + stage.SpawnDistance;
        return true;
    }

    // ── 시간 ──

    // 대기(초)를 단계 수로: n × 단계 길이 ≥ 대기인 가장 작은 n. 흐름은 단계 경계에서만 판단하므로 대기는 단계 단위다.
    // 데이터가 double이면 단계 길이의 정수 배(0.5초 = 25단계)가 정확히 그 단계 수가 된다(0~400초의 0.02초 배수 전부 확인).
    // float로 받으면 절반쯤이 한 단계 늘어서(0.1f → 6단계) 흐름 시간 데이터는 double이다.
    private long StepsFor(double seconds)
    {
        if (!(seconds > 0.0)) return 0;
        double h = clock.StepLength;
        double ratio = seconds / h;
        if (!(ratio < MaxWaitSteps)) return MaxWaitSteps; // 무한대·거대한 값: 사실상 끝나지 않는다(long 넘침 방지)

        long n = (long)Math.Ceiling(ratio);
        while (n > 0 && (n - 1) * h >= seconds) n--;
        while (n * h < seconds) n++;
        return n;
    }

    private static FlowTimings Sanitize(FlowTimings t)
    {
        // 데이터 검사(에디터·빌드)가 먼저 막는다. 여기는 그래도 들어온 값으로 멈추지 않게 하고 숨기지 않고 알린다.
        return new FlowTimings(
            ValidWait(t.EncounterClearDelay, "encounterClearDelay"),
            ValidWait(t.BossIntroDelay, "bossIntroDelay"),
            ValidWait(t.TransitionDuration, "transitionDuration"));
    }

    private static double ValidWait(double seconds, string name)
    {
        if (seconds >= 0.0 && !double.IsInfinity(seconds)) return seconds;
        GameLog.Error($"[EncounterDirector] 흐름 대기 {name}이 잘못됐습니다: {seconds}. 0으로 진행합니다.");
        return 0.0;
    }

    // ── 진전 감시 (R4) ──

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    private void WatchProgress(long now)
    {
        long last = Math.Max(world.LastProgressStep, lastFlowProgressStep);
        if (last == stallReportedFor) return;
        if ((now - last) * clock.StepLength < StallSeconds) return;

        stallReportedFor = last;
        Post(Notice.Stalled(DescribeStall(now)));
    }

    private string DescribeStall(long now)
    {
        var text = new StringBuilder();
        text.Append($"[EncounterDirector] 게임 시간 {StallSeconds:0}초 동안 진전(피해, 사망, 이동, 흐름 변화)이 없습니다. ");
        text.Append($"스테이지 {(stage != null ? stage.StageNumber : 0)}, 단계 {phaseNow}, 시각 {now * clock.StepLength:0.00}초.");
        for (int i = 0; i < world.Combatants.Count; i++)
        {
            Combatant c = world.Combatants[i];
            text.Append($"\n  {c.Team} {c.Order}: {c.Phase}, x {c.X:0.###}, HP {c.Hp}/{c.MaxHp}, 공격력 {c.AttackDamage}, 사거리 {c.AttackRange}, 대상 {(c.Target != null ? c.Target.Order.ToString() : "없음")}");
        }
        return text.ToString();
    }
#endif
}
