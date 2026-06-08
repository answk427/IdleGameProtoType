using UnityEngine;

public class UVScroller : MonoBehaviour
{
    [SerializeField]
    private float speed = 0.5f; // 스크롤 속도

    private Material mat;      // 매터리얼을 담을 변수
    private Vector2 offset; // 오프셋 값을 기억해 둘 변수

    public bool IsActiveScroll { get; private set; } = true;

    void Start()
    {
        // 내 Quad에 입혀진 매터리얼을 가져옴
        mat = GetComponent<Renderer>().material;

        GameManager.Instance.OnScrollStateChanged += UpdateScrollState;
    }

    // Update 대신 LateUpdate 사용!
    void LateUpdate()
    {
        if (!IsActiveScroll)
        {
            return;
        }

        // 1. 오프셋 값을 증가시킴
        offset.x += speed * Time.deltaTime;

        // 2. 핵심: 값이 무한히 커지지 않고 0 ~ 1 사이만 계속 맴돌게 강제 리셋 (오차 방지)
        offset.x = Mathf.Repeat(offset.x, 1f);

        // 3. 매터리얼에 적용
        mat.mainTextureOffset = offset;
    }

    // GameManager가 다음 스테이지로 넘어갈 때 호출
    public void ChangeTexture(Texture2D newTexture)
    {
        if (mat != null && newTexture != null)
        {
            mat.mainTexture = newTexture;
            offset.x = 0f;
            mat.mainTextureOffset = offset;
        }
    }

    private void UpdateScrollState(bool isScrolling)
    {
        IsActiveScroll = isScrolling;
    }


}
