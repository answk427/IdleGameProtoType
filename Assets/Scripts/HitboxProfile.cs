using UnityEngine;
using UnityEngine.Serialization;

// 판정 몸(docs/COMBAT_ARCHITECTURE.md 8-4, D9). 몸의 중심 = 피벗 + 오프셋, 반너비는 필수 값이다.
// 판정(교전, 대상, 범위)과 이펙트 위치가 같은 몸을 쓴다.
//
// 예전에는 반너비가 0이면 스프라이트 바운드로 자동 계산했는데, 그 값은 애니메이션 프레임마다 달라져서(공격 모션의 여백 등)
// 판정이 프레임에 따라 흔들렸다. 그래서 자동 계산을 지우고, 0 이하이면 에디터·빌드 검사가 에러를 낸다.
[System.Serializable]
public class HitboxProfile
{
    [Tooltip("판정 몸의 반너비(월드 유닛). 0보다 커야 한다")]
    [FormerlySerializedAs("halfWidthOverride")]
    [SerializeField] private float halfWidth = 0f;

    [Tooltip("피벗에서 몸 중심까지의 오프셋. x는 판정과 이펙트에, y는 이펙트 높이에 쓴다")]
    [FormerlySerializedAs("hitPositionOffset")]
    [SerializeField] private Vector2 bodyOffset = Vector2.zero;

    public float HalfWidth => halfWidth;
    public Vector2 BodyOffset => bodyOffset;
}
