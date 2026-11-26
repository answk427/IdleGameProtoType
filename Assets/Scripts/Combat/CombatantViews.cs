using System;
using System.Collections.Generic;

// 전투원 → 그 전투원을 그리는 호스트(뷰)의 표(5-A). 전투 규칙(Combatant)은 자기 뷰를 모르고,
// 뷰가 필요한 쪽(사건 배달, 화면 보간, 카메라, 이펙트 위치, 풀 반납)은 여기서 찾는다.
//
// 사건을 받아 그 사건의 주인(Source)인 전투원의 뷰에 넘긴다. 다른 구독자(이펙트, 보상)는 월드의 사건을 직접 구독한다.
// 호스트는 스포너가 만들 때 등록하고(풀에서 다시 꺼내도 같은 전투원이라 한 번이면 된다), 파괴될 때 빠진다.
public sealed class CombatantViews : IDisposable
{
    private readonly Dictionary<Combatant, CombatantHost> byCombatant = new Dictionary<Combatant, CombatantHost>();
    private readonly List<CombatantHost> all = new List<CombatantHost>();
    private readonly IDisposable subscription;

    public CombatantViews(Observable<CombatEvent> events)
    {
        subscription = events.Subscribe(Route);
    }

    public IReadOnlyList<CombatantHost> All => all;

    public void Register(CombatantHost host)
    {
        if (host == null || host.Combatant == null || byCombatant.ContainsKey(host.Combatant)) return;

        byCombatant.Add(host.Combatant, host);
        all.Add(host);
        host.BindViews(this);
    }

    public void Unregister(CombatantHost host)
    {
        if (host == null || host.Combatant == null) return;
        if (byCombatant.Remove(host.Combatant)) all.Remove(host);
    }

    public bool TryGet(Combatant combatant, out CombatantHost host)
    {
        host = null;
        return combatant != null && byCombatant.TryGetValue(combatant, out host) && host != null;
    }

    public void Dispose() => subscription.Dispose();

    private void Route(CombatEvent e)
    {
        if (e.Source != null && TryGet(e.Source, out CombatantHost host)) host.HandleEvent(e);
    }
}
