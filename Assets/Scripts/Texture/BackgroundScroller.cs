using UnityEngine;

// 배경(docs/COMBAT_ARCHITECTURE.md 9-3). 카메라를 따라 화면을 덮고, 텍스처는 월드가 이동한 거리만큼 흐른다(패럴랙스).
// 시뮬레이션과 무관한 그림이라 고정 단계가 아니라 화면 프레임(CameraRig.LateUpdate)에서 그린다.
// 그래서 플레이어가 달리면 그 속도로 흐르고, 멈추면 멈춘다(예전의 "스크롤 중" 상태가 필요 없다).
// 스테이지가 바뀌면 그 스테이지의 배경 텍스처로 바꾼다.
public class BackgroundScroller : MonoBehaviour
{
    [Tooltip("월드 이동 거리(유닛)를 텍스처 오프셋(0~1)으로 바꾸는 배율(패럴랙스). 텍스처·쿼드 크기에 따라 배경마다 조정한다")]
    [SerializeField] private float scrollScale = 0.25f;

    private Material mat;

    private void Awake()
    {
        mat = GetComponent<Renderer>().material;
        GlobalGameEvents.OnStageChanged += HandleStageChanged;
    }

    private void OnDestroy()
    {
        GlobalGameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void HandleStageChanged(int stageNumber)
    {
        StageEntry entry = GameDatabaseManager.Instance != null ? GameDatabaseManager.Instance.GetStage(stageNumber) : null;
        if (entry != null) SetBackground(entry.backgroundTexture);
    }

    public void SetBackground(Texture2D newBg)
    {
        if (newBg != null)
        {
            mat.mainTextureOffset = mat.mainTextureOffset; // 오프셋은 이동 거리에서 다시 계산되므로 그대로 둔다
            mat.mainTexture = newBg;
        }
    }

    // CameraRig가 화면 프레임마다 부른다. worldDistance = 월드가 처음부터 이동한 거리(원점 재설정과 무관하게 이어진다).
    public void Follow(float cameraX, double worldDistance)
    {
        Vector3 position = transform.position;
        position.x = cameraX;
        transform.position = position;

        double offset = worldDistance * scrollScale % 1.0;
        if (offset < 0.0) offset += 1.0;
        mat.mainTextureOffset = new Vector2((float)offset, mat.mainTextureOffset.y);
    }
}
