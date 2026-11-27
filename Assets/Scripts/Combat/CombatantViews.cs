using System;
using System.Collections.Generic;

// 전투원 → 그 전투원을 그리는 호스트(뷰)의 표(5-A, docs/COMBAT_ARCHITECTURE.md 12절 D44). 전투 규칙(Combatant)은 자기 뷰를 모르고,
// 뷰가 필요한 쪽(사건 배달, 화면 보간, 카메라, 이펙트 위치)은 여기서 찾는다.
//
// 월드의 사건을 받아 붙이고 떼고 넘긴다:
//   Joined: 아직 뷰가 없으면 attach로 호스트를 붙인다(BattleView가 프리팹을 풀에서 꺼낸다). 그 뒤 호스트에 넘긴다.
//   Left: 호스트에 넘긴 뒤 표에서 빼고 detach로 뗀다(풀로 돌려놓는다).
//   그 밖: 그 사건의 주인(Source)인 전투원의 호스트에 넘긴다.
// 풀에서 다시 쓴 전투원은 같은 객체라, 배달 순서(Left → Joined)대로 옛 뷰가 떨어지고 새 뷰가 붙는다(같은 단계 안이어도).
// 다른 구독자(이펙트, 보상)는 월드의 사건을 직접 구독한다.
public sealed class CombatantViews : IDisposable
{
    private readonly Dictionary<Combatant, CombatantHost> byCombatant = new Dictionary<Combatant, CombatantHost>();
    private readonly List<CombatantHost> all = new List<CombatantHost>();
    private readonly Func<Combatant, CombatantHost> attach;
    private readonly Action<Combatant, CombatantHost> detach;
    private readonly IDisposable subscription;

    public CombatantViews(Observable<CombatEvent> events, Func<Combatant, CombatantHost> attach, Action<Combatant, CombatantHost> detach)
    {
        this.attach = attach ?? throw new ArgumentNullException(nameof(attach));
        this.detach = detach ?? throw new ArgumentNullException(nameof(detach));
        subscription = events.Subscribe(Route);
    }

    public IReadOnlyList<CombatantHost> All => all;

    public bool TryGet(Combatant combatant, out CombatantHost host)
    {
        host = null;
        return combatant != null && byCombatant.TryGetValue(combatant, out host) && host != null;
    }

    // 호스트가 파괴될 때(풀을 비울 때 등) 빠진다.
    public void Forget(CombatantHost host)
    {
        if (host == null) return;
        int index = all.IndexOf(host);
        if (index < 0) return;
        all.RemoveAt(index);
        foreach (KeyValuePair<Combatant, CombatantHost> pair in byCombatant)
        {
            if (pair.Value != host) continue;
            byCombatant.Remove(pair.Key);
            break;
        }
    }

    public void Dispose() => subscription.Dispose();

    private void Route(CombatEvent e)
    {
        Combatant source = e.Source;
        if (source == null) return;

        if (e.Type == CombatEventType.Joined && !byCombatant.ContainsKey(source))
        {
            CombatantHost attached = attach(source);
            if (attached == null) return;
            byCombatant.Add(source, attached);
            all.Add(attached);
            attached.BindViews(this);
        }

        if (!TryGet(source, out CombatantHost host)) return;
        host.HandleEvent(e);

        if (e.Type == CombatEventType.Left)
        {
            byCombatant.Remove(source);
            all.Remove(host);
            detach(source, host);
        }
    }
}
