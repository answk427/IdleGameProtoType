using System.Collections;
using TMPro;
using UnityEngine;

// 이 녀석도 풀(Pool)에서 꺼내 쓸 거니까 풀링 매니저가 부르는 함수가 필요함
public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    
    private System.Action<GameObject> onComplete; // 반납용 콜백

    private void Awake()
    {
        if (text == null)   
            text = GetComponent<TextMeshProUGUI>();
    }

    public void Play(
    int damage,
    Vector3 worldPos,
    Canvas canvas,
    RectTransform canvasRect,
    System.Action<GameObject> returnAction)
    {
        StopAllCoroutines();

        onComplete = returnAction;

        text.text = damage.ToString();
        text.color = new Color(text.color.r, text.color.g, text.color.b, 1f);

        Vector2 screenPoint = Camera.main.WorldToScreenPoint(worldPos);

        Camera uiCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay
            ? null
            : canvas.worldCamera;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect,
            screenPoint,
            uiCamera,
            out Vector2 localPoint
        );

        ((RectTransform)transform).anchoredPosition = localPoint;

        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float duration = 1f;
        float elapsed = 0f;

        RectTransform rect = (RectTransform)transform;
        Vector2 startPos = rect.anchoredPosition;
        Color startColor = text.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;

            rect.anchoredPosition = startPos + Vector2.up * (50f * t);
            text.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);

            yield return null;
        }

        onComplete?.Invoke(gameObject);
    }
}