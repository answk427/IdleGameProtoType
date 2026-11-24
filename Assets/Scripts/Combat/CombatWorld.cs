using System;
using System.Collections.Generic;

// 전투원 전체와 그 사이의 규칙(거리, 대상, 처리 순서)을 갖는다(docs/COMBAT_ARCHITECTURE.md 8-3, 8-4). 순수 C#.
// CombatSimulation(드라이버)이 고정 단계마다 국면 순서로 부른다: ② RunEvents → ③ StepPhases → ⑤ CaptureStep.
//
// 처리 순서는 아군 → 적, 각 팀 안에서는 등록 순서다. 같은 시각의 전투 사건(CombatScheduler)도 이 순서다.
// 처리 도중의 등록·해제(사망 알림을 받은 흐름 코드가 부르는 경우)는 그 국면이 끝난 뒤에 반영한다.
public sealed class CombatWorld
{
    private readonly List<Combatant> combatants = new List<Combatant>(16);
    private readonly List<Combatant> pendingAdd = new List<Combatant>(4);
    private readonly List<Combatant> pendingRemove = new List<Combatant>(4);
    private readonly List<CombatActions> participants = new List<CombatActions>(16);
    private readonly CombatScheduler scheduler = new CombatScheduler();
    private int nextOrder;
    private bool busy;

    public CombatWorld(SimClock clock)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public SimClock Clock { get; }
    public IReadOnlyList<Combatant> Combatants => combatants;

    // 아군(전진 정책)이 이동 단계인가. 3단계 전까지 "세상이 스크롤되는가"의 뜻이다.
    // ③에서 아군을 먼저 처리하므로, 같은 단계의 적 이동이 이 값을 읽는다.
    public bool IsAllyAdvancing { get; private set; }

    // 스크롤과 접근 이동의 속도. 3단계 전까지는 전진하는 아군(플레이어)의 이동 속도다.
    public float ScrollSpeed { get; private set; }

    // ── 등록 ──

    public void Add(Combatant combatant)
    {
        if (combatant == null) throw new ArgumentNullException(nameof(combatant));
        if (combatant.World != null) throw new InvalidOperationException("이미 월드에 등록된 전투원입니다.");

        combatant.World = this;
        combatant.Order = nextOrder++;
        if (busy) pendingAdd.Add(combatant);
        else combatants.Add(combatant);
    }

    public void Remove(Combatant combatant)
    {
        if (combatant == null || combatant.World != this) return;
        if (busy)
        {
            if (!pendingRemove.Contains(combatant)) pendingRemove.Add(combatant);
            return;
        }
        RemoveNow(combatant);
    }

    private void RemoveNow(Combatant combatant)
    {
        combatants.Remove(combatant);
        pendingAdd.Remove(combatant);
        combatant.Combat.Disengage();
        combatant.World = null;
    }

    private void Flush()
    {
        for (int i = 0; i < pendingRemove.Count; i++) RemoveNow(pendingRemove[i]);
        pendingRemove.Clear();

        for (int i = 0; i < pendingAdd.Count; i++) combatants.Add(pendingAdd[i]);
        pendingAdd.Clear();
    }

    // ── 고정 단계 ──

    // ② 전투 사건. 교전 중인 전투원의 행동·쿨다운 사건을 단계 안의 정확한 시각 순서로 처리한다.
    public void RunEvents()
    {
        busy = true;
        try
        {
            participants.Clear();
            AddParticipants(Team.Ally);
            AddParticipants(Team.Enemy);
            scheduler.RunStep(Clock, participants);
        }
        finally
        {
            busy = false;
            Flush();
        }
    }

    // ③ 단계 전이와 이동.
    public void StepPhases()
    {
        busy = true;
        try
        {
            Combatant leader = FindAdvancingAlly();
            ScrollSpeed = leader != null ? leader.MoveSpeed : 0f;
            IsAllyAdvancing = false;

            StepTeam(Team.Ally, leader);
            StepTeam(Team.Enemy, leader);
        }
        finally
        {
            busy = false;
            Flush();
        }
    }

    // ⑤ 보간용 기록
    public void CaptureStep()
    {
        for (int i = 0; i < combatants.Count; i++) combatants[i].CaptureStep();
    }

    private void AddParticipants(Team team)
    {
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (c.Team == team && c.Combat.IsActive) participants.Add(c.Combat);
        }
    }

    private void StepTeam(Team team, Combatant leader)
    {
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (c.Team != team) continue;

            c.StepPhase();
            if (c == leader) IsAllyAdvancing = c.Phase == CombatantPhase.Moving;
        }
    }

    private Combatant FindAdvancingAlly()
    {
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (c.Team == Team.Ally && c.MovePolicy == MovePolicy.Advance && c.Phase != CombatantPhase.Inactive) return c;
        }
        return null;
    }

    // ── 거리와 대상 (규칙 하나: 표면 거리) ──

    // 두 몸 사이의 거리. 겹치면 음수다.
    public static float Gap(Combatant a, Combatant b) => Math.Abs(a.X - b.X) - a.HalfWidth - b.HalfWidth;

    // 반대 팀, 살아 있음, 표면 거리가 가장 가까움. 앞뒤를 구분하지 않는다. 같으면 등록 순번이 빠른 쪽.
    public Combatant FindNearestOpponent(Combatant self)
    {
        Combatant best = null;
        float bestGap = float.PositiveInfinity;
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (!IsOpponent(self, c)) continue;

            float gap = Gap(self, c);
            if (gap < bestGap)
            {
                best = c;
                bestGap = gap;
            }
        }
        return best;
    }

    // 표면 거리가 range 이하인 살아 있는 상대를 into에 더한다(버퍼는 부르는 쪽이 재사용한다).
    public void CollectOpponentsInRange(Combatant self, float range, List<Combatant> into)
    {
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (IsOpponent(self, c) && Gap(self, c) <= range) into.Add(c);
        }
    }

    private static bool IsOpponent(Combatant self, Combatant other) =>
        other != self && other.Team != self.Team && other.IsAlive;
}
