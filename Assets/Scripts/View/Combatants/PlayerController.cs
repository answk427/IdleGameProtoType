using UnityEngine;

// 플레이어 캐릭터의 뷰(호스트, docs/COMBAT_ARCHITECTURE.md 8-3). 그리기만 한다.
// 전투원, 능력치(진행도 + 캐릭터 표), 스킬 명령과 쿨다운은 게임 쪽 PlayerCombat이 갖는다(12절 D45).
// BattleView가 플레이어 전투원의 Joined에 이 프리팹을 꺼내 붙인다.
public class PlayerController : CombatantHost
{
    [Tooltip("스프라이트 피벗이 발 기준이 아닐 때, 발이 바닥(PlayAreaBounds.GroundY)에 닿아 보이도록 " +
             "보정하는 값(월드 유닛). MonsterEntry.groundOffset과 동일한 역할 — 캐릭터(직업)별로 다른 " +
             "스프라이트를 쓸 수 있으므로 PlayAreaBounds 쪽 전역값이 아니라 여기서 캐릭터별로 보정한다.")]
    [SerializeField] private float groundOffset = 0f;
    public float GroundOffset => groundOffset;

    // 달리기 동작이 있는 캐릭터. 이동 단계면 달린다.
    protected override void HandleLocomotion(bool moving)
    {
        if (view != null) view.PlayLocomotion(moving);
    }
}
