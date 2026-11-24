using UnityEngine;

// 배경 텍스처 스크롤(러닝 중 UV 오프셋 이동) + 스테이지 전환 시 배경 텍스처 교체를 함께 담당.
public class BackgroundScroller : MonoBehaviour
{
    // 플레이어 RunSpeed(월드 단위/초)를 UV 오프셋(0~1) 변화량으로 환산하는 배율.
    // 텍스처/쿼드 크기에 따라 달라지므로 배경마다 튜닝 가능하게 남겨둔다.
    [SerializeField]
    private float scrollScale = 0.25f;

    private Material mat;      // 매터리얼을 담을 변수

    // 보간용 직전/현재 고정 단계의 UV 오프셋 x (docs/COMBAT_DESIGN.md 3-3)
    private float previousOffsetX;
    private float currentOffsetX;

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

    // CombatSimulation이 고정 단계마다 부른다. 몬스터와 같은 단계에서 같은 속도로 움직여야 서로 미끄러져 보이지 않는다.
    public void Tick(SimClock clock)
    {
        previousOffsetX = currentOffsetX;
        if (!IsActiveScroll) return;

        float runSpeed = GameManager.Instance != null ? GameManager.Instance.GetPlayer()?.RunSpeed ?? 0f : 0f;
        currentOffsetX += runSpeed * scrollScale * (float)clock.StepLength;

        // 1을 넘기면 둘에서 똑같이 빼서, 보간이 0~1 사이를 거꾸로 훑지 않게 한다.
        if (currentOffsetX >= 1f)
        {
            currentOffsetX -= 1f;
            previousOffsetX -= 1f;
        }
    }

    // CombatSimulation이 화면 프레임마다 부른다.
    public void ApplyRenderOffset(float alpha)
    {
        float x = Mathf.Repeat(Mathf.LerpUnclamped(previousOffsetX, currentOffsetX, alpha), 1f);
        mat.mainTextureOffset = new Vector2(x, mat.mainTextureOffset.y);
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
