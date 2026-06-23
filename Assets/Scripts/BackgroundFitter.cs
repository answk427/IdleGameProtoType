using UnityEngine;

// PlayAreaBounds(플레이 가능 영역)를 항상 정확히 덮도록 배경 Quad의 위치/크기를 자동으로 맞춘다.
//
// bottomUiScreenRatio 등 PlayAreaBounds의 설정값이 바뀌면, 이 컴포넌트가 매번 재계산해서
// 따라가므로 배경을 손으로 다시 맞출 필요가 없다.
//
// 전제: 이 오브젝트는 기본 Quad 메쉬(로컬 크기 1x1, 중심 피벗)를 사용한다.
[ExecuteAlways]
public class BackgroundFitter : MonoBehaviour
{
    [SerializeField] private Camera targetCamera;
    [SerializeField] private PlayAreaBounds playArea;

    [Tooltip("좌우로 추가로 더 덮을 여유 비율. 0.1 = 카메라가 보여주는 가로 폭보다 10% 더 넓게.")]
    [SerializeField] private float horizontalPadding = 0f;

    private void OnEnable()
    {
        Fit();
    }

#if UNITY_EDITOR
    private void Update()
    {
        // 에디터에서 Inspector 값을 조정하는 동안에도 즉시 반영되도록.
        // 플레이 모드에서는 매 프레임 재계산할 필요가 없으므로 에디터 전용으로 제한.
        if (!Application.isPlaying)
        {
            Fit();
        }
    }
#endif

    public void Fit()
    {
        if (targetCamera == null) targetCamera = Camera.main;
        if (playArea == null) playArea = PlayAreaBounds.Instance ?? FindAnyObjectByType<PlayAreaBounds>();
        if (targetCamera == null || playArea == null) return;

        float halfHeight = targetCamera.orthographicSize;
        float halfWidth = halfHeight * targetCamera.aspect;

        float minY = playArea.MinPlayableY;
        float maxY = playArea.MaxPlayableY;

        float playAreaHeight = maxY - minY;
        float playAreaCenterY = (minY + maxY) / 2f;

        float requiredWidth = (halfWidth * 2f) * (1f + horizontalPadding);

        Vector3 pos = transform.position;
        transform.position = new Vector3(0f, playAreaCenterY, pos.z);
        transform.localScale = new Vector3(requiredWidth, playAreaHeight, 1f);
    }
}
