using System;
using System.Collections;
using UnityEngine;

public class RewardDropEffect : MonoBehaviour
{
    private const float GravityArcHeight = 0.8f;

    private SpriteRenderer spriteRenderer;
    private Coroutine playRoutine;

    private Action<GameObject> onComplete;

    // 튀어나와 떨어지는 궤적의 시작과 끝(월드). 원점 재설정 때 같이 옮긴다.
    private Vector3 startPosition;
    private Vector3 endPosition;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        if (spriteRenderer == null)
        {
            spriteRenderer = gameObject.AddComponent<SpriteRenderer>();
        }
    }

    public void Play(
        Sprite sprite,
        Color color,
        Vector3 startPosition,
        Vector3 landingOffset,
        float duration,
        float size,
        int sortingOrder,
        bool rotate,
        Action<GameObject> returnAction)
    {
        if (playRoutine != null)
        {
            StopCoroutine(playRoutine);
        }

        spriteRenderer.sprite = sprite;
        spriteRenderer.color = color;
        spriteRenderer.sortingOrder = sortingOrder;
        transform.position = startPosition;
        transform.localScale = Vector3.one * size;

        onComplete = returnAction;
        this.startPosition = startPosition;
        endPosition = startPosition + landingOffset;
        playRoutine = StartCoroutine(PlayRoutine(duration, rotate));

    }

    // 원점 재설정(CombatEffectManager.ShiftOrigin): 궤적과 지금 위치를 같은 양만큼 옮긴다.
    public void ShiftOrigin(float delta)
    {
        startPosition.x -= delta;
        endPosition.x -= delta;
        Vector3 position = transform.position;
        position.x -= delta;
        transform.position = position;
    }

    private IEnumerator PlayRoutine(float duration, bool rotate)
    {
        float elapsed = 0f;
        Color startColor = spriteRenderer.color;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            Vector3 position = Vector3.Lerp(startPosition, endPosition, t);
            position.y += Mathf.Sin(t * Mathf.PI) * GravityArcHeight;
            transform.position = position;

            float squash = 1f + Mathf.Sin(t * Mathf.PI) * 0.18f;
            transform.localScale = new Vector3(transform.localScale.x, squash * Mathf.Abs(transform.localScale.x), 1f);

            if (rotate)
            {
                transform.Rotate(0f, 0f, 420f * Time.deltaTime);
            }

            yield return null;
        }

        yield return new WaitForSeconds(0.25f);

        elapsed = 0f;
        const float fadeDuration = 0.35f;
        while (elapsed < fadeDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            spriteRenderer.color = new Color(startColor.r, startColor.g, startColor.b, 1f - t);
            transform.position += Vector3.up * (Time.deltaTime * 0.25f);
            yield return null;
        }

        onComplete.Invoke(gameObject);
    }
}
