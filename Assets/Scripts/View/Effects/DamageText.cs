using System.Collections;
using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    
    private System.Action<GameObject> onComplete; // 반납용 콜백

    // 숫자가 뜬 월드 위치. 카메라가 움직여도 월드를 따라가도록 매 프레임 화면 좌표로 바꾼다(D21).
    private Vector3 worldAnchor;
    private Canvas canvas;
    private RectTransform canvasRect;

    private void Awake()
    {
        if (text == null)   
            text = GetComponent<TextMeshProUGUI>();
    }

    // prefix: 데미지는 보통 빈 문자열, 힐은 "+"처럼 구분되게 붙여서 색만으로도 헷갈리지 않게 한다.
    public void Play(
    GameNumber amount,
    Vector3 worldPos,
    Canvas canvas,
    RectTransform canvasRect,
    System.Action<GameObject> returnAction,
    Color color,
    string prefix = "")
    {
        StopAllCoroutines();

        onComplete = returnAction;

        text.text = prefix + NumberFormatUtility.Format(amount);
        text.color = new Color(color.r, color.g, color.b, 1f);

        worldAnchor = worldPos;
        this.canvas = canvas;
        this.canvasRect = canvasRect;
        ((RectTransform)transform).anchoredPosition = AnchorOnCanvas();

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float duration = 1f;
        float elapsed = 0f;

        RectTransform rect = (RectTransform)transform;
        Color startColor = text.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rect.anchoredPosition = AnchorOnCanvas() + Vector2.up * (50f * t);
            text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);

            yield return null;
        }

        onComplete?.Invoke(gameObject);
    }

    // 원점 재설정(CombatEffectManager.ShiftOrigin): 월드 기준점을 같은 양만큼 옮긴다.
    public void ShiftOrigin(float delta) => worldAnchor.x -= delta;

    private Vector2 AnchorOnCanvas()
    {
        Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldAnchor);
        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, screenPoint, uiCamera, out Vector2 localPoint);
        return localPoint;
    }
}
