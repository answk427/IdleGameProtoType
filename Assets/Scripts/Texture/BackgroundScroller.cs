using UnityEngine;

// 배경 텍스처 스크롤(러닝 중 UV 오프셋 이동) + 스테이지 전환 시 배경 텍스처 교체를 함께 담당.
public class BackgroundScroller : MonoBehaviour
{
    // 플레이어 RunSpeed(월드 단위/초)를 UV 오프셋(0~1) 변화량으로 환산하는 배율.
    // 텍스처/쿼드 크기에 따라 달라지므로 배경마다 튜닝 가능하게 남겨둔다.
    [SerializeField]
    private float scrollScale = 0.25f;

    private Material mat;      // 매터리얼을 담을 변수
    private Vector2 offset; // 오프셋 값을 기억해 둘 변수

    public bool IsActiveScroll { get; private set; } = true;

    void Awake()
    {
        // GameManager.Start()가 최초 스테이지 진입 시 이벤트를 쏴도
        // Awake에서 이미 구독을 완료한 상태가 보장
        mat = GetComponent<Renderer>().material;
        GlobalGameEvents.OnStageChanged += HandleStageChanged;
        GlobalGameEvents.OnScrollChanged += UpdateScrollState;
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
            mat.mainTexture = newBg;
        }
    }

    void LateUpdate()
    {
        if (!IsActiveScroll)
        {
            return;
        }

        float runSpeed = GameManager.Instance != null ? GameManager.Instance.GetPlayer()?.Stats.RunSpeed ?? 0f : 0f;
        offset.x += runSpeed * scrollScale * Time.deltaTime;

        offset.x = Mathf.Repeat(offset.x, 1f);

        mat.mainTextureOffset = offset;
    }

    private void OnDestroy()
    {
        GlobalGameEvents.OnScrollChanged -= UpdateScrollState;
        GlobalGameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void UpdateScrollState(bool isScrolling)
    {
        IsActiveScroll = isScrolling;
    }
}
