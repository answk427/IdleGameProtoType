using UnityEngine;
using UnityEngine.Serialization;

// 그림의 몸 기준점(docs/COMBAT_ARCHITECTURE.md 12절 D50). 피벗(스프라이트의 발 기준)에서 몸 중심까지의 오프셋이다.
// 그림을 시뮬레이션의 위치(몸 중심)에 맞추는 데만 쓴다: 피벗 = 몸 중심 − x, 이펙트 높이 = 피벗 + y.
// 전투는 이것을 읽지 않는다. 판정 몸의 반너비는 게임 데이터(몬스터·캐릭터 표의 halfWidth)다.
// (예전 HitboxProfile. 반너비가 프리팹에 있어 아트 쪽 값이 교전 거리를 정했다.)
[System.Serializable]
public class BodyAnchor
{
    [Tooltip("피벗에서 몸 중심까지의 오프셋. x는 그림을 몸 중심에 맞추고, y는 이펙트(타격, 스킬) 높이에 쓴다")]
    [FormerlySerializedAs("hitPositionOffset")]
    [SerializeField] private Vector2 bodyOffset = Vector2.zero;

    public Vector2 BodyOffset => bodyOffset;
}
