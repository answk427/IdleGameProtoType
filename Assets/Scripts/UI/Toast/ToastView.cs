using TMPro;
using UnityEngine;

// 토스트의 View(프리팹). 글을 보이고 나타나기/사라지기 연출만 한다. 언제 보이고 언제 사라질지는 ToastService가 정한다.
// UIRoot가 만든 토스트 층(UISortBand.Toast) 밑에 하나만 생긴다. 그 층에는 GraphicRaycaster가 없어 터치를 가로채지 않는다.
//
// 창이 아니라 늘 있는 층이라 연출은 Update(화면 시간)로 돈다. 보이지 않을 때는 아무것도 하지 않는다.
public class ToastView : MonoBehaviour, IToastView
{
    [SerializeField] private CanvasGroup group;
    [SerializeField] private TMP_Text messageText;

    [Header("연출")]
    [Tooltip("마지막으로 보인 때부터 사라지기 시작할 때까지(초)")]
    [SerializeField] private float displaySeconds = 1.6f;
    [SerializeField] private float fadeSeconds = 0.15f;
    [Tooltip("나타날 때(연타 포함) 잠깐 커지는 정도")]
    [SerializeField] private float popScale = 1.08f;

    private float targetAlpha;
    private float scale = 1f;

    public float DisplaySeconds => displaySeconds;

    private void Awake()
    {
        if (group != null)
        {
            group.alpha = 0f;
            group.interactable = false;
            group.blocksRaycasts = false;
        }
        targetAlpha = 0f;
    }

    public void Show(string message)
    {
        if (messageText != null) messageText.text = message;
        targetAlpha = 1f;
        scale = popScale;
        enabled = true;
    }

    public void Hide()
    {
        targetAlpha = 0f;
        enabled = true;
    }

    private void Update()
    {
        if (group == null) return;

        float step = fadeSeconds > 0f ? Time.unscaledDeltaTime / fadeSeconds : 1f;
        group.alpha = Mathf.MoveTowards(group.alpha, targetAlpha, step);
        scale = Mathf.MoveTowards(scale, 1f, step * Mathf.Abs(popScale - 1f));
        transform.localScale = new Vector3(scale, scale, 1f);

        // 다 나타났거나 다 사라졌으면 쉰다(Show/Hide가 다시 켠다).
        if (Mathf.Approximately(group.alpha, targetAlpha) && Mathf.Approximately(scale, 1f)) enabled = false;
    }
}
