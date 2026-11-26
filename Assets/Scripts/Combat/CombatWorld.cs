using System;
using System.Collections.Generic;

// 전투원 전체와 그 사이의 규칙(거리, 대상, 처리 순서)을 갖는다(docs/COMBAT_ARCHITECTURE.md 8-3, 8-4). 순수 C#.
// CombatSimulation(드라이버)이 고정 단계마다 국면 순서로 부른다:
//   (① 흐름) → ② RunEvents → ③ StepPhases → ⑤ CaptureStep → ⑥ DeliverEvents.
// 위치는 월드 x다(3단계). 좌표가 커지지 않게 인카운터를 스폰할 때마다 원점을 재설정한다(ShiftOrigin).
//
// 처리 순서는 아군 → 적, 각 팀 안에서는 등록 순서다. 같은 시각의 전투 사건(CombatScheduler)도 이 순서다.
// 처리 도중의 등록·해제는 그 국면이 끝난 뒤에 반영한다.
// 흐름(스폰, 정리, 원점 재설정)은 처리 밖인 국면 ①(EncounterDirector)에서 한다.
//
// 바깥(뷰, 이펙트, 보상)에는 사건(CombatEvent)으로만 알린다(5-A, P7). 사건은 일어날 때 버퍼에 쌓이고, 드라이버가 단계가 끝난 뒤
// DeliverEvents로 배달한다. 배달 중에 생긴 사건(구독자가 전투원을 스폰하는 등)은 다음 배달로 간다.
// 단계마다 배달하는 이유: 풀에서 다시 꺼낸 전투원은 같은 객체라, 지난 사건이 다음 단계의 재스폰 뒤에 도착하면 새 몬스터에 잘못 간다.
public sealed class CombatWorld
{
    private readonly List<Combatant> combatants = new List<Combatant>(16);
    private readonly List<Combatant> pendingAdd = new List<Combatant>(4);
    private readonly List<Combatant> pendingRemove = new List<Combatant>(4);
    private readonly List<CombatActions> participants = new List<CombatActions>(16);
    private readonly CombatScheduler scheduler = new CombatScheduler();
    private readonly Subject<CombatEvent> events = new Subject<CombatEvent>();
    private List<CombatEvent> pendingEvents = new List<CombatEvent>(64);
    private List<CombatEvent> deliveringEvents = new List<CombatEvent>(64);
    private int nextOrder;
    private bool busy;
    private bool delivering;

    public CombatWorld(SimClock clock)
    {
        Clock = clock ?? throw new ArgumentNullException(nameof(clock));
    }

    public SimClock Clock { get; }
    public IReadOnlyList<Combatant> Combatants => combatants;

    // 원점 재설정으로 뺀 거리의 합. 월드가 처음부터 실제로 이동한 거리 = 지금 좌표 + 이 값(배경의 연속성).
    public double TotalShift { get; private set; }

    // 전투 사건. 드라이버가 DeliverEvents를 부를 때 일어난 순서대로 온다.
    public Observable<CombatEvent> Events => events;

    // 아직 배달하지 않은 사건 수(테스트·진단용).
    public int PendingEventCount => pendingEvents.Count;

    internal void Emit(in CombatEvent e) => pendingEvents.Add(e);

    // ⑥ 사건 배달. 처리 밖에서만 부른다(드라이버의 단계 끝, 단계 밖의 명령 뒤). 버퍼를 바꿔 끼우므로
    // 배달 중에 생긴 사건은 이번 순회에 섞이지 않고 다음 배달로 간다.
    public void DeliverEvents()
    {
        if (busy) throw new InvalidOperationException("고정 단계 처리 중에는 사건을 배달하지 않습니다.");
        if (delivering || pendingEvents.Count == 0) return;

        List<CombatEvent> batch = pendingEvents;
        pendingEvents = deliveringEvents;
        deliveringEvents = batch;

        delivering = true;
        try
        {
            for (int i = 0; i < batch.Count; i++) events.OnNext(batch[i]);
        }
        finally
        {
            batch.Clear();
            delivering = false;
        }
    }

    // 대열의 앞장(전진 정책의 첫 아군, 지금은 플레이어). 카메라와 스폰 위치의 기준이다.
    public Combatant LeadAlly
    {
        get
        {
            for (int i = 0; i < combatants.Count; i++)
            {
                Combatant c = combatants[i];
                if (c.Team == Team.Ally && c.MovePolicy == MovePolicy.Advance && c.Phase != CombatantPhase.Inactive) return c;
            }
            return null;
        }
    }

    // 마지막으로 진전(피해 > 0, 사망, 이동)이 있었던 단계. 흐름의 진전 감시(EncounterDirector)가 본다.
    public long LastProgressStep { get; private set; }

    internal void NoteProgress() => LastProgressStep = Clock.StepIndex;

    // ── 팀 (흐름이 지난 단계의 결과를 묻는다) ──

    public int CountAlive(Team team)
    {
        int count = 0;
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (c.Team == team && c.IsAlive) count++;
        }
        return count;
    }

    // 그 팀이 하나 이상 있고, 모두 죽어서 사망 행동까지 마쳤다(아군 전멸).
    public bool IsDefeated(Team team)
    {
        bool any = false;
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (c.Team != team || c.Phase == CombatantPhase.Inactive) continue;
            if (!c.IsDeathComplete) return false;
            any = true;
        }
        return any;
    }

    // ── 등록 ──

    public void Add(Combatant combatant)
    {
        if (combatant == null) throw new ArgumentNullException(nameof(combatant));
        if (combatant.World != null) throw new InvalidOperationException("이미 월드에 등록된 전투원입니다.");

        combatant.World = this;
        combatant.Order = nextOrder++;
        if (busy) pendingAdd.Add(combatant);
        else combatants.Add(combatant);

        // 뷰가 처음 상태(HP, 이동 동작)를 그린다. 스폰은 보통 등록 전에 일어나서 그때의 사건은 월드가 없어 남지 않는다.
        Emit(CombatEvent.Joined(Clock.Now, combatant));
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
            StepTeam(Team.Ally);
            StepTeam(Team.Enemy);
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

    // 원점 재설정(D16): 모든 위치에서 delta를 뺀다. 처리 밖(흐름 국면 ①의 스폰)에서만 부른다.
    // 거리, 대상, 단계는 그대로이고(모두 상대 위치), 그려지는 위치도 끊기지 않는다(보간 기준도 같이 옮김).
    public void ShiftOrigin(float delta)
    {
        if (busy) throw new InvalidOperationException("고정 단계 처리 중에는 원점을 옮길 수 없습니다.");
        if (delta == 0f || float.IsNaN(delta) || float.IsInfinity(delta)) return;

        for (int i = 0; i < combatants.Count; i++) combatants[i].ShiftOrigin(delta);
        TotalShift += delta;
        Emit(CombatEvent.OriginShifted(Clock.Now, delta)); // 뷰가 살아 있는 연출을 같은 양만큼 옮긴다
    }

    // anchor가 원점 근처(0 이상 1 미만)에 오도록 정수만큼 옮긴다.
    public void RecenterOn(Combatant anchor)
    {
        if (anchor == null || anchor.World != this) return;
        ShiftOrigin((float)Math.Floor(anchor.X));
    }

    private void AddParticipants(Team team)
    {
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (c.Team == team && c.Combat.IsActive) participants.Add(c.Combat);
        }
    }

    private void StepTeam(Team team)
    {
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (c.Team == team) c.StepPhase();
        }
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

    // mover가 direction(+1 / -1) 쪽으로 움직일 때 막는 접촉선: 그 방향(중심이 같거나 앞)의 살아 있는 상대 중 가장 가까운 것.
    // 없으면 그 방향의 무한대.
    public float FindBlockingContact(Combatant mover, float direction)
    {
        float best = direction > 0f ? float.PositiveInfinity : float.NegativeInfinity;
        for (int i = 0; i < combatants.Count; i++)
        {
            Combatant c = combatants[i];
            if (!IsOpponent(mover, c) || direction * (c.X - mover.X) < 0f) continue;

            float contact = c.X - direction * (c.HalfWidth + mover.HalfWidth);
            if (direction > 0f ? contact < best : contact > best) best = contact;
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
