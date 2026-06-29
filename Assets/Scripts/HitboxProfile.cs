using UnityEngine;

// 사거리 판정용 반너비(HalfWidth)와 이펙트 스폰 위치 보정값을 한 곳에 모은 합성용 데이터.
[System.Serializable]
public class HitboxProfile
{
    // 스프라이트 셀에는 공격 모션 등을 위한 여백이 포함돼 있어 자동 계산(SpriteRenderer/Collider 바운드)이
    // 실제 캐릭터 폭보다 훨씬 크게 잡히는 문제가 있다. 0보다 크면 이 값을 그대로 반너비로 사용한다.
    [SerializeField] private float halfWidthOverride = 0f;

    // 공격 모션 등으로 손/무기가 뻗는 프레임은 그 프레임만 트림된 스프라이트 영역이 비대칭이라
    // SpriteRenderer.bounds.center가 재생 중인 애니메이션 프레임에 따라 들쭉날쭉해진다(halfWidthOverride와 같은 이유).
    // 그래서 자동 계산 대신 디자이너가 직접 "몸통 중앙"으로 보정하는 고정 오프셋을 둔다.
    [SerializeField] private Vector2 hitPositionOffset = Vector2.zero;

    public float GetHalfWidth(GameObject target) =>
        halfWidthOverride > 0f ? halfWidthOverride : CombatRangeUtility.GetHalfWidth(target);

    public Vector3 GetPosition(Transform t) => t.position + (Vector3)hitPositionOffset;
}
