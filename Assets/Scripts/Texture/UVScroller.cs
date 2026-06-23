using UnityEngine;

public class UVScroller : MonoBehaviour
{
    // 플레이어 RunSpeed(월드 단위/초)를 UV 오프셋(0~1) 변화량으로 환산하는 배율.
    // 텍스처/쿼드 크기에 따라 달라지므로 배경마다 튜닝 가능하게 남겨둔다.
    [SerializeField]
    private float scrollScale = 0.25f;

    private Material mat;      // 매터리얼을 담을 변수
    private Vector2 offset; // 오프셋 값을 기억해 둘 변수

    public bool IsActiveScroll { get; private set; } = true;

    void Start()
    {
        GlobalGameEvents.OnStageChanged += HandleStageChanged;
        GlobalGameEvents.OnScrollChanged += UpdateScrollState;

        // 내 Quad에 입혀진 매터리얼을 가져옴
        mat = GetComponent<Renderer>().material;
    }

    private void HandleStageChanged(StageData newStage)
    {
        if (!string.IsNullOrEmpty(newStage.bgTexturePath))
        {
            Texture2D newBg = Resources.Load<Texture2D>(newStage.bgTexturePath);
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
