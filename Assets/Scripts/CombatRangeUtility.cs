using UnityEngine;

// 사거리 판정에 쓸 "캐릭터 반너비"를 계산하는 공용 유틸리티.
// 중심점 사이 거리만으로 사거리를 재면, 덩치가 큰 몬스터의 경우 실제 몸 안으로
// 파고들어야 공격 판정이 나는 문제가 생긴다. SpriteRenderer 바운드(시각적 몸체 크기)를
// 우선 사용하고, 스프라이트가 없을 때만 Collider2D 바운드로 대체한다.
// 주의: GroundSensor/WallSensor 같은 감지용 트리거 콜라이더가 캐릭터에 붙어 있는 경우
// Collider2D를 먼저 쓰면 그 작은 센서 크기가 잡혀서 반너비가 실제보다 훨씨 작게
// 계산되는 문제가 있었음 (HeroKnight의 반지름 0.05 센서들이 대표적 사례).
public static class CombatRangeUtility
{
    public static float GetHalfWidth(GameObject target)
    {
        if (target == null) return 0f;

        SpriteRenderer spriteRenderer = target.GetComponentInChildren<SpriteRenderer>();
        if (spriteRenderer != null) return spriteRenderer.bounds.extents.x;

        Collider2D collider = target.GetComponentInChildren<Collider2D>();
        if (collider != null) return collider.bounds.extents.x;

        return 0f;
    }
}
