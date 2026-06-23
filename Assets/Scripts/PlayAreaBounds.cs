using UnityEngine;

// 카메라(Orthographic)가 보여주는 월드 영역과, 화면 하단 UI가 차지하는 비율을 기준으로
// '몬스터/플레이어가 위치해야 하는 플레이 가능 영역(월드 좌표)'을 계산하는 유틸리티.
//
// 화면 하단 UI 비율(BottomUIRoot)이 바뀌면 이 한 곳의 bottomUiScreenRatio 값만 수정하면
// MonsterSpawner, PlayerController 등 위치를 다루는 모든 코드가 자동으로 맞춰진다.
public class PlayAreaBounds : MonoBehaviour
{
    public static PlayAreaBounds Instance;

    [SerializeField] private Camera targetCamera;

    [Tooltip("화면 하단 UI(BottomUIRoot)가 차지하는 비율. 0.35 = 화면 세로의 35%")]
    [SerializeField] private float bottomUiScreenRatio = 0.35f;

    [Tooltip("화면 상단에 남겨둘 여백 비율 (선택, 상단 HUD 등과 겹치지 않도록)")]
    [SerializeField] private float topUiScreenRatio = 0f;

    [Tooltip("바닥(MinPlayableY)에서 캐릭터 발 위치까지 추가로 띄울 여백(월드 유닛). 피벗이 발이면 보통 0.")]
    [SerializeField] private float groundOffset = 0f;

    private void Awake()
    {
        Instance = this;
        if (targetCamera == null) targetCamera = Camera.main;
    }

    // 카메라가 보여주는 전체 세로 절반 높이 (orthographicSize)
    private float HalfHeight => targetCamera.orthographicSize;
    private float HalfWidth => HalfHeight * targetCamera.aspect;

    // 플레이 가능 영역의 월드 Y 최저값 (이 아래는 하단 UI 영역)
    public float MinPlayableY => -HalfHeight + (HalfHeight * 2f * bottomUiScreenRatio);

    // 플레이 가능 영역의 월드 Y 최고값 (상단 여백 고려)
    public float MaxPlayableY => HalfHeight - (HalfHeight * 2f * topUiScreenRatio);

    // 카메라가 보여주는 월드 X 범위 (좌우는 보통 스폰 anchor로 별도 관리하지만 참고용으로 제공)
    public float MinPlayableX => -HalfWidth;
    public float MaxPlayableX => HalfWidth;

    // 플레이어/몬스터가 '항상 바닥에 서 있는 것처럼' 보이도록 쓰는 고정 Y값.
    // 범위(Min~Max) 안 임의 위치가 아니라, 이 한 값만 사용한다.
    public float GroundY => MinPlayableY + groundOffset;

    // 주어진 월드 Y가 플레이 가능 영역(=UI에 가려지지 않는 영역) 안에 있는지 검사
    public bool IsWithinPlayableArea(float worldY)
    {
        return worldY >= MinPlayableY && worldY <= MaxPlayableY;
    }

    // 월드 Y를 플레이 가능 영역 안으로 강제 클램프
    public float ClampToPlayableY(float worldY)
    {
        return Mathf.Clamp(worldY, MinPlayableY, MaxPlayableY);
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (targetCamera == null) return;

        float halfW = HalfWidth;
        float minY = MinPlayableY;
        float maxY = MaxPlayableY;

        // 플레이 가능 영역의 상/하단 경계선
        Gizmos.color = Color.green;
        Gizmos.DrawLine(new Vector3(-halfW, minY, 0), new Vector3(halfW, minY, 0));
        Gizmos.DrawLine(new Vector3(-halfW, maxY, 0), new Vector3(halfW, maxY, 0));

        // 하단 UI에 가려지는 영역을 반투명 빨간 박스로 표시
        float bottomUiHeight = minY - (-HalfHeight);
        Vector3 bottomUiCenter = new Vector3(0, -HalfHeight + bottomUiHeight / 2f, 0);
        Vector3 bottomUiSize = new Vector3(halfW * 2f, bottomUiHeight, 0.1f);
        Gizmos.color = new Color(1f, 0f, 0f, 0.25f);
        Gizmos.DrawCube(bottomUiCenter, bottomUiSize);

        // 상단 여백 영역(있다면) 표시
        if (topUiScreenRatio > 0f)
        {
            float topUiHeight = HalfHeight - maxY;
            Vector3 topUiCenter = new Vector3(0, HalfHeight - topUiHeight / 2f, 0);
            Vector3 topUiSize = new Vector3(halfW * 2f, topUiHeight, 0.1f);
            Gizmos.DrawCube(topUiCenter, topUiSize);
        }
    }
#endif
}
