using System;
using System.Collections.Generic;

// 전투 행동을 하는 쪽(Combatant)이 제공해야 하는 것. 테스트는 가짜 시전자로 이 규칙만 따로 확인한다.
// 시각은 모두 시뮬레이션 시계의 절대 시각(초)이다.
public interface ICombatActor
{
    bool IsAlive { get; }

    bool TryGetTiming(string key, out ActionTiming timing);

    // 기본 공격이 있는가(데이터: 공격 간격 > 0). 없으면 스킬만 쓴다.
    bool HasBasicAttack { get; }

    // 기본 공격 한 번의 길이(= 공격 간격). 공식은 각 시전자의 스탯이 정한다.
    double BasicAttackDuration { get; }

    // 지금 사거리 안에 행동할 대상이 있으면 그 대상을 현재 대상으로 잡고 true. 없으면 false(새 행동을 시작하지 않는다).
    bool TryAcquireTarget();

    // now 시각에 준비된 스킬 중 우선순위가 가장 높은 것. 없으면 null.
    Skill PickReadySkill(double now);

    // now 이후 처음으로 결정 입력이 바뀌는 시각(지금은 장착 스킬이 준비되는 시각). 없으면 무한대.
    // 시간에 따라 바뀌는 결정 입력(예: 나중의 버프 만료)이 생기면 이 함수에만 더한다.
    double NextDecisionInputChange(double now);

    // 타격 순간에 대상을 다시 확인한다(D3): 살아 있고 사거리 안이 아니면 사거리 안에서 다시 찾고, 없으면 헛친다.
    void ApplyBasicAttackHit();
    void ApplySkillHit(Skill skill);

    // 연출 훅. skill은 기본 공격이면 null.
    void OnActionStarted(ActionTiming action, double duration, Skill skill);
    void OnActionsIdle();
}

// 전투원 하나의 행동 결정과 진행(docs/COMBAT_ARCHITECTURE.md 7-3, 7-11, 7-13). 플레이어와 보스가 같이 쓴다.
// 시간은 스스로 흘리지 않는다. CombatScheduler가 모든 전투원의 사건을 시각 순서대로 불러 준다:
//   NextEventTime(지금) → 그 시각으로 시계 이동 → FireEventsAt(t)(모두) → DecideAt(t)(모두)
//
// 규칙:
//   - **취소 없음.** 모든 행동(기본 공격, 스킬)은 끝까지 한다. 다음 행동은 지금 행동이 완료된 뒤에만 정한다.
//     어떤 행동도 다른 행동을 끊지 못한다. 행동을 끊는 것은 사망 같은 강제 중단(Disengage)뿐이다.
//   - 행동의 길이는 게임 데이터가 정한다: 기본 공격 = 시전자의 공격 간격, 스킬 = SkillSpec.CastTime. 애니메이션은 타격 비율만 정한다.
//   - 다음 행동: 수동 요청 → 준비된 스킬(우선순위) → 기본 공격. 스킬은 시작하는 순간 쿨다운이 시작된다.
//   - 결정 입력이 바뀌는 시각(행동이 없을 때의 스킬 준비, 요청한 스킬 준비)도 사건이다. 그 시각에 정확히 다시 결정한다.
public sealed class CombatActions : IActionListener
{
    private readonly ICombatActor actor;
    private readonly ActionRunner runner = new ActionRunner();
    private readonly HashSet<string> reportedProblems = new HashSet<string>();

    private Skill currentSkill;   // 진행 중인 행동이 스킬이면 그 스킬, 기본 공격이면 null
    private Skill requestedSkill; // UI가 요청한 스킬. 준비돼 있고 대상이 있는 다음 결정 시점에 쓴다
    private bool wasBusy;

    public CombatActions(ICombatActor actor)
    {
        this.actor = actor ?? throw new ArgumentNullException(nameof(actor));
    }

    // 교전 상태가 켜고 끈다. 교전 중이고 살아 있을 때만 스케줄러가 사건을 처리한다.
    public bool IsEngaged { get; private set; }
    public bool IsActive => IsEngaged && actor.IsAlive;

    // 행동이 없을 때만 다른 일(다음 행동, 달리기 복귀)을 할 수 있다(취소 없음).
    public bool IsFree => !runner.IsRunning;
    public ActionTiming CurrentAction => runner.Current;
    public double CurrentActionStartTime => runner.StartTime;

    public void RequestSkill(Skill skill) => requestedSkill = skill;
    public void ClearRequest() => requestedSkill = null;

    public void Engage() => IsEngaged = true;

    // 교전을 떠날 때(달리기 복귀, 사망). 진행 중인 행동은 버린다. 수동 요청은 남긴다.
    public void Disengage()
    {
        IsEngaged = false;
        runner.Cancel();
        currentSkill = null;
        wasBusy = false;
    }

    // now 이후 이 전투원에게 일어날 다음 사건의 시각. 없으면 무한대.
    public double NextEventTime(double now)
    {
        double next = runner.NextMarkerTime;
        if (IsFree)
        {
            next = Math.Min(next, After(now, actor.NextDecisionInputChange(now)));
            if (requestedSkill != null) next = Math.Min(next, After(now, requestedSkill.ReadyAt));
        }
        return next;
    }

    // t까지(포함)의 행동 표식(타격, 완료)을 처리한다.
    public void FireEventsAt(double t) => runner.FireMarkersUpTo(t, this);

    // t 시각의 결정. 행동이 없을 때만 다음 행동을 정한다.
    public void DecideAt(double t)
    {
        if (IsFree) TryStartNext(t);

        if (wasBusy && !runner.IsRunning) actor.OnActionsIdle();
        wasBusy = runner.IsRunning;
    }

    private bool TryStartNext(double t)
    {
        if (!actor.TryAcquireTarget()) return false;

        Skill skill = TakeRequest(t) ?? actor.PickReadySkill(t);
        if (skill != null && TryResolve(skill.Spec.ActionKey, out ActionTiming skillAction)
            && HasValidCastTime(skill))
        {
            double castTime = skill.Spec.CastTime;
            skill.BeginCooldown(t);
            currentSkill = skill;
            runner.Start(skillAction, castTime, t);
            actor.OnActionStarted(skillAction, castTime, skill);
            return true;
        }

        if (!actor.HasBasicAttack || !TryResolve(ActionKeys.Attack, out ActionTiming attack)) return false;

        double duration = actor.BasicAttackDuration;
        if (!IsValidDuration(duration, "attack-duration", "기본 공격 길이(공격 간격)")) return false;

        currentSkill = null;
        runner.Start(attack, duration, t);
        actor.OnActionStarted(attack, duration, null);
        return true;
    }

    // 메시지 문자열은 실패했을 때만 만든다(통과하는 경로에서 할당하지 않게, R6).
    private bool IsValidDuration(double duration, string problemKey, string what)
    {
        if (IsPositiveFinite(duration)) return true;
        Report(problemKey, $"[CombatActions] {what}이 잘못됐습니다: {duration}. 0보다 커야 합니다.");
        return false;
    }

    private bool HasValidCastTime(Skill skill)
    {
        if (IsPositiveFinite(skill.Spec.CastTime)) return true;
        Report("cast-time:" + skill.Spec.Id,
            $"[CombatActions] 스킬 {skill.Spec.Id}({skill.Spec.Name})의 시전 시간(castTime)이 잘못됐습니다: {skill.Spec.CastTime}. 0보다 커야 합니다.");
        return false;
    }

    private static bool IsPositiveFinite(double value) => value > 0.0 && !double.IsInfinity(value);

    private Skill TakeRequest(double t)
    {
        Skill skill = requestedSkill;
        if (skill == null || !skill.IsReadyAt(t)) return null;
        requestedSkill = null;
        return skill;
    }

    private bool TryResolve(string key, out ActionTiming timeline)
    {
        if (!actor.TryGetTiming(key, out timeline))
        {
            Report("missing:" + key, $"[CombatActions] '{key}' 행동 시간표가 없습니다. 데이터 항목의 행동 시간표(timelines)를 확인하세요.");
            return false;
        }

        string error = timeline.ValidationError;
        if (error != null)
        {
            Report("invalid:" + key, $"[CombatActions] '{key}' 행동 시간표가 잘못됐습니다: {error}. 굽기 도구로 다시 구우세요.");
            timeline = null;
            return false;
        }

        return true;
    }

    // now보다 뒤인 시각만 사건이다(지금 시각의 입력은 이미 이번 결정에 반영됐다).
    private static double After(double now, double time) => time > now ? time : double.PositiveInfinity;

    // 같은 문제를 매번 찍지 않게 한 번만 남긴다. 숨기지는 않는다.
    private void Report(string problemKey, string message)
    {
        if (reportedProblems.Add(problemKey)) GameLog.Error(message);
    }

    void IActionListener.OnActionHit(ActionTiming action, int hitIndex)
    {
        if (currentSkill != null) actor.ApplySkillHit(currentSkill);
        else actor.ApplyBasicAttackHit();
    }

    void IActionListener.OnActionCompleted(ActionTiming action)
    {
        currentSkill = null;
    }
}
