using System.Collections;
using TMPro;
using UnityEngine;

public class DamageText : MonoBehaviour
{
    [SerializeField] private TextMeshProUGUI text;
    
    private System.Action<GameObject> onComplete; // 반납용 콜백

    private void Awake()
    {
        if (text == null)   
            text = GetComponent<TextMeshProUGUI>();
    }

    // prefix: 데미지는 보통 빈 문자열, 힐은 "+"처럼 구분되게 붙여서 색만으로도 헷갈리지 않게 한다.
    public void Play(
    int amount,
    Vector3 worldPos,
    Canvas canvas,
    RectTransform canvasRect,
    System.Action<GameObject> returnAction,
    Color color,
    string prefix = "")
    {
        StopAllCoroutines();

        onComplete = returnAction;

        text.text = prefix + amount;
        text.color = new Color(color.r, color.g, color.b, 1f);

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