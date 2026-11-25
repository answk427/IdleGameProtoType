using System;
using System.Collections.Generic;
using UnityEngine;

// 전투원 하나(docs/COMBAT_ARCHITECTURE.md 8절). 플레이어, 일반 몬스터, 보스가 모두 이 클래스다. 순수 C#.
//
// - 위치(X)는 판정 몸의 중심이고, 월드 x다(3단계, docs/COMBAT_ARCHITECTURE.md 9절). 로직은 transform을 읽지 않는다.
//   호스트가 화면 프레임마다 직전 두 단계 사이를 보간해 그리고, 카메라가 아군을 따라간다.
// - 이동은 sweep 하나: 그 방향의 가장 가까운 살아 있는 상대 몸(접촉선)까지만 간다. 전진(아군)과 접근(보스)이 같은 함수다.
// - 거리는 표면 거리 하나(CombatWorld.Gap). 교전, 대상, 범위 스킬이 모두 이것을 쓴다.
// - 상태는 단계 값 하나(이동 / 교전 / 사망). 상태 객체를 만들지 않는다.
// - 행동의 결정과 진행은 CombatActions(1단계)가 하고, 이 클래스가 그것이 요구하는 것(ICombatActor)을 제공한다.
// - 기본 공격 능력은 데이터다: 공격 간격 > 0이면 있다. 기본 공격도 스킬도 없으면 교전하지 않는다.
public sealed class Combatant : ICombatActor, IActionListener
{
    private readonly ICombatantHost host;
    private readonly ActionRunner deathRunner = new ActionRunner();
    private readonly List<Combatant> hitBuffer = new List<Combatant>(8);

    private CombatantStats stats;
    private IActionTimelineSource timelines;
    private SkillLoadout skills;
    private bool deathCompletionReported;

    // 보간용 직전/현재 고정 단계의 위치
    private float previousStepX;
    private float stepX;

    public Combatant(Team team, ICombatantHost host)
    {
        Team = team;
        this.host = host ?? throw new ArgumentNullException(nameof(host));
        Combat = new CombatActions(this);
        Phase = CombatantPhase.Inactive;
    }

    public Team Team { get; }
    public ICombatantHost Host => host;
    public CombatActions Combat { get; }

    // 등록한 월드와 등록 순번(같은 팀 안의 처리 순서, 같은 거리의 대상 순서). CombatWorld가 정한다.
    public CombatWorld World { get; internal set; }
    public int Order { get; internal set; }

    public CombatantPhase Phase { get; private set; }
    public bool IsAlive => Phase == CombatantPhase.Moving || Phase == CombatantPhase.Engaged;
    // 죽었고 사망 행동(시간표)까지 마쳤다. 흐름이 아군 전멸을 판단한다.
    public bool IsDeathComplete => Phase == CombatantPhase.Dead && deathCompletionReported;
    public Combatant Target { get; private set; }

    public float X { get; private set; }
    public float HalfWidth { get; private set; }

    public GameNumber Hp { get; private set; }
    public GameNumber MaxHp => stats.MaxHp;
    public GameNumber AttackDamage => stats.AttackDamage;
    public double AttackInterval => stats.AttackInterval;
    public float AttackRange => stats.AttackRange;
    public float MoveSpeed => stats.MoveSpeed;
    public MovePolicy MovePolicy => stats.MovePolicy;
    public SkillLoadout Skills => skills;

    public bool HasBasicAttack => stats.AttackInterval > 0.0 && !double.IsInfinity(stats.AttackInterval);
    // 행동할 수단(기본 공격이나 스킬)이 있어야 교전한다.
    public bool CanAct => HasBasicAttack || (skills != null && skills.HasAnySkill);

    // ── 생애 ──

    // 스폰(풀에서 다시 꺼낼 때 포함). 같은 객체를 다시 쓰므로 이전 상태를 모두 지운다.
    public void Spawn(in CombatantStats newStats, float x, float halfWidth, IActionTimelineSource timelineSource, SkillLoadout skillLoadout)
    {
        stats = Sanitize(newStats);
        timelines = timelineSource;
        skills = skillLoadout;

        HalfWidth = halfWidth >= 0f && !float.IsInfinity(halfWidth) ? halfWidth : ReportAndUse(0f, $"반너비가 잘못됐습니다: {halfWidth}");
        Place(x);

        Hp = stats.MaxHp;
        Target = null;
        deathRunner.Cancel();
        deathCompletionReported = false;
        Combat.Disengage();
        Combat.ClearRequest();

        host.HandleHpChanged(this);
        EnterMoving();
    }

    // 능력치만 바꾼다(업그레이드, 레벨업, 튜닝). 현재 HP는 유지하고 새 최대치를 넘지 않게 한다.
    public void UpdateStats(in CombatantStats newStats)
    {
        stats = Sanitize(newStats);
        if (Hp > stats.MaxHp) Hp = stats.MaxHp;
        host.HandleHpChanged(this);
    }

    // 위치를 직접 정한다(스폰, 순간이동). 보간 기준도 같이 맞춰서 예전 자리에서 미끄러져 오지 않게 한다.
    public void Place(float x)
    {
        X = x;
        previousStepX = x;
        stepX = x;
    }

    public void TakeDamage(GameNumber amount)
    {
        if (!IsAlive) return;
        if (!(amount.ToDouble() >= 0.0)) amount = GameNumber.Zero; // 음수·NaN 데미지는 0으로

        Hp = GameNumber.Max(Hp - amount, GameNumber.Zero);
        if (amount > GameNumber.Zero) World?.NoteProgress();
        host.HandleDamaged(this, amount);

        if (Hp <= GameNumber.Zero) Die();
    }

    public void Heal(GameNumber amount)
    {
        if (!IsAlive) return;
        if (!(amount.ToDouble() >= 0.0)) amount = GameNumber.Zero;

        Hp = GameNumber.Min(Hp + amount, stats.MaxHp);
        host.HandleHealed(this, amount);
    }

    // 스테이지를 다시 시작할 때(흐름이 부른다). 살아 있어도 교전 중이던 행동을 버리고 처음 상태(이동)로 돌아간다.
    public void Revive()
    {
        if (Phase == CombatantPhase.Inactive) return;

        Combat.Disengage();
        deathRunner.Cancel();
        deathCompletionReported = false;
        Target = null;
        Hp = stats.MaxHp;
        host.HandleRevived(this);
        EnterMoving();
    }

    private void Die()
    {
        Combat.ClearRequest();
        Combat.Disengage();
        Target = null;
        Phase = CombatantPhase.Dead;
        deathCompletionReported = false;
        World?.NoteProgress();

        // 사망 시간표는 기다릴 일이 있는 전투원(지금은 플레이어)만 갖는다. 사망 시각(사건 처리 중의 정확한 시각)부터 진행한다.
        if (World != null && timelines != null && timelines.TryGet(ActionKeys.Death, out ActionTimeline death) && death.IsValid)
            deathRunner.Start(death, death.ClipLength, World.Clock.Now);

        host.HandleDied(this);
    }

    // ── 고정 단계 ③: 단계 전이와 이동 (CombatWorld가 아군 → 적, 각 팀은 등록 순으로 부른다) ──

    internal void StepPhase()
    {
        switch (Phase)
        {
            case CombatantPhase.Moving:
                // 움직인 뒤에 대상을 본다(움직여서 사거리에 들어오면 같은 단계에 교전).
                Move();
                if (TryAcquireTarget())
                {
                    Phase = CombatantPhase.Engaged;
                    host.HandleLocomotion(this, false);
                    Combat.Engage(); // 첫 결정은 다음 단계 시작(같은 순간)
                }
                break;

            case CombatantPhase.Engaged:
                // 취소 없음: 지금 행동이 끝났고 대상이 없을 때만 이동으로 돌아간다.
                if (Combat.IsFree && !TryAcquireTarget())
                {
                    Combat.Disengage();
                    EnterMoving();
                }
                break;

            case CombatantPhase.Dead:
                AdvanceDeath();
                break;
        }
    }

    // ⑤: 보간용 기록
    internal void CaptureStep()
    {
        previousStepX = stepX;
        stepX = X;
    }

    // 원점 재설정(CombatWorld.ShiftOrigin). 위치와 보간 기준을 같은 양만큼 옮겨서, 그려지는 위치가 끊기지 않는다.
    internal void ShiftOrigin(float delta)
    {
        X -= delta;
        previousStepX -= delta;
        stepX -= delta;
    }

    public float GetRenderX(float alpha) => previousStepX + (stepX - previousStepX) * alpha;

    private void EnterMoving()
    {
        Phase = CombatantPhase.Moving;
        host.HandleLocomotion(this, true);
    }

    private void AdvanceDeath()
    {
        if (deathCompletionReported) return;

        if (deathRunner.IsRunning && World != null) deathRunner.FireMarkersUpTo(World.Clock.Now, this);
        if (deathRunner.IsRunning) return;

        // 알림은 러너 처리가 끝난 뒤에 보낸다(받는 쪽이 부활시켜도 러너가 처리 중이 아니게).
        deathCompletionReported = true;
        host.HandleDeathCompleted(this);
    }

    private void Move()
    {
        if (World == null || !(stats.MoveSpeed > 0f)) return;

        float distance = stats.MoveSpeed * (float)World.Clock.StepLength;
        switch (stats.MovePolicy)
        {
            case MovePolicy.Advance:
                Sweep(1f, distance); // 앞(+x)으로
                return;
            case MovePolicy.Hold:
                return;
            case MovePolicy.Approach:
                Combatant other = World.FindNearestOpponent(this);
                if (other != null) Sweep(other.X >= X ? 1f : -1f, distance);
                return;
        }
    }

    // sweep(CCD의 일반화, docs/COMBAT_DESIGN.md 3-4): direction 쪽으로 distance만큼 가되, 그 방향의 가장 가까운
    // 살아 있는 상대의 접촉선(두 몸이 닿는 위치)에서 멈춘다. 이미 겹쳐 있으면 더 파고들지 않는다(뒤로 밀려나지도 않는다).
    // 한 단계 이동량과 상관없이 몸을 통과하지 않는다. 시체는 막지 않는다.
    private void Sweep(float direction, float distance)
    {
        float next = X + direction * distance;
        float contact = World.FindBlockingContact(this, direction);

        float moved = direction > 0f
            ? Mathf.Min(next, Mathf.Max(X, contact))
            : Mathf.Max(next, Mathf.Min(X, contact));
        if (moved != X) World.NoteProgress();
        X = moved;
    }

    // ── 대상 ──

    // 지금 대상이 유효하면 그대로(떨림 방지), 아니면 가장 가까운 상대를 다시 찾는다(D3, D11).
    private Combatant AcquireTarget()
    {
        if (World == null) return Target = null;
        if (IsValidTarget(Target)) return Target;

        Combatant nearest = World.FindNearestOpponent(this);
        Target = IsValidTarget(nearest) ? nearest : null;
        return Target;
    }

    private bool IsValidTarget(Combatant other) =>
        other != null && other.IsAlive && other.Team != Team && other.World == World &&
        CombatWorld.Gap(this, other) <= stats.AttackRange;

    // ── 효과 ──

    public GameNumber ComputeDamage(double multiplier) => GameNumber.Round((stats.AttackDamage * multiplier).ToDouble());

    // ── ICombatActor ──

    public bool TryGetTimeline(string key, out ActionTimeline timeline)
    {
        timeline = null;
        return timelines != null && timelines.TryGet(key, out timeline);
    }

    double ICombatActor.BasicAttackDuration => stats.AttackInterval;

    public bool TryAcquireTarget() => IsAlive && CanAct && AcquireTarget() != null;

    Skill ICombatActor.PickReadySkill(double now) => skills?.PickReady(now);

    double ICombatActor.NextDecisionInputChange(double now) => skills != null ? skills.NextReadyTime(now) : double.PositiveInfinity;

    void ICombatActor.ApplyBasicAttackHit()
    {
        Combatant target = AcquireTarget();
        if (target == null) return; // 헛침

        target.TakeDamage(ComputeDamage(1.0));
        host.HandleBasicAttackHit(this, target);
    }

    void ICombatActor.ApplySkillHit(Skill skill)
    {
        // 단일 대상 효과는 대상을 다시 확인하고, 범위·자기 효과는 대상이 없어도 적용된다(효과 구현이 판단).
        hitBuffer.Clear();
        skill.Apply(this, AcquireTarget(), hitBuffer);
        host.HandleSkillHit(this, skill, hitBuffer);
    }

    void ICombatActor.OnActionStarted(ActionTimeline action, double duration, Skill skill) =>
        host.HandleActionStarted(this, action, duration, skill);

    void ICombatActor.OnActionsIdle() => host.HandleLocomotion(this, false);

    // 사망 러너의 알림(표식 처리 중). 완료 여부는 AdvanceDeath가 러너 상태로 확인한다.
    void IActionListener.OnActionHit(ActionTimeline action, int hitIndex) { }
    void IActionListener.OnActionCompleted(ActionTimeline action) { }

    // ── 값 검사 ──
    // 데이터 검사(에디터·빌드)가 먼저 막는다. 여기는 그래도 들어온 값으로 진행이 멈추지 않게 하고, 숨기지 않고 알린다.

    private CombatantStats Sanitize(CombatantStats s)
    {
        if (!(s.MaxHp.ToDouble() > 0.0)) s.MaxHp = ReportAndUse((GameNumber)1, $"최대 HP가 잘못됐습니다: {s.MaxHp}");
        if (!(s.AttackDamage.ToDouble() >= 0.0)) s.AttackDamage = ReportAndUse(GameNumber.Zero, $"공격력이 잘못됐습니다: {s.AttackDamage}");
        if (double.IsNaN(s.AttackInterval)) s.AttackInterval = ReportAndUse(0.0, "공격 간격이 NaN입니다");
        if (!(s.AttackRange >= 0f)) s.AttackRange = ReportAndUse(0f, $"사거리가 잘못됐습니다: {s.AttackRange}");
        if (!(s.MoveSpeed >= 0f)) s.MoveSpeed = ReportAndUse(0f, $"이동 속도가 잘못됐습니다: {s.MoveSpeed}");
        return s;
    }

    private T ReportAndUse<T>(T fallback, string message)
    {
        Debug.LogError($"[Combatant] {Team} {Order}: {message}. {fallback}(으)로 진행합니다.");
        return fallback;
    }
}
