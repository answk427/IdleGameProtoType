using UnityEngine;

// 몬스터의 뷰(호스트). 일반 몬스터와 보스가 모두 이 클래스다(docs/COMBAT_ARCHITECTURE.md 8-3).
// 전투원과 그 능력치·처치 보상은 시뮬레이션(EnemyRoster)이 만들고, 이 뷰는 BattleView가 붙여 그리기만 한다(12절 D44).
// 둘의 차이는 데이터다: 공격 간격 > 0이면 기본 공격, skillId가 있으면 스킬, movePolicy가 Approach면 다가온다.
public class Monster : CombatantHost
{
    [SerializeField] private FloatingHpBar hpBar;

    protected override void Awake()
    {
        base.Awake();
        if (hpBar == null) hpBar = GetComponentInChildren<FloatingHpBar>();
    }

    public override void Bind(IReadOnlyCombatant combatant)
    {
        base.Bind(combatant);

        // 체력바가 먼저 켜져 있어야 등록(Joined) 때 HP 알림을 그린다. 죽었던 몬스터를 풀에서 다시 꺼낸 경우도 같다.
        if (hpBar != null) hpBar.gameObject.SetActive(true);
    }

    protected override void HandleDied()
    {
        base.HandleDied();

        // 시체는 인카운터가 끝날 때까지 화면에 남으므로 빈 체력바를 숨긴다.
        // 보상 지급은 사망 사건을 받은 Game이, 동전 연출은 CombatEffectManager가 한다.
        if (hpBar != null) hpBar.gameObject.SetActive(false);
    }
}
