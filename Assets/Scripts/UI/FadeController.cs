using System.Collections;
using System;
using UnityEngine;

public class FadeController : MonoBehaviour
{
    public static FadeController Instance;

    [SerializeField] private CanvasGroup fadeCanvasGroup;

    private InputBlocker inputBlocker;

    // 코루틴은 중간에 끊기면 finally가 돌지 않으므로 지역변수가 아니라 이 객체가 들고 OnDisable에서도 푼다.
    private IDisposable inputLease;

    // 그리는 순서(System 구분)와 입력 차단은 UIRoot가 준다.
    public void Initialize(InputBlocker blocker)
    {
        inputBlocker = blocker;
    }

    private void OnDisable() => ReleaseInput();

    private void Awake()
    {
        // 싱글톤 중복 생성 방지
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false;
        }
    }

    public IEnumerator FadeOut(float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        if (inputLease == null) inputLease = inputBlocker?.Acquire("ScreenFade", showSpinner: false);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsed / duration);
            yield return null;
        }

        fadeCanvasGroup.alpha = 1f;
    }

    public IEnumerator FadeIn(float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsed / duration));
            yield return null;
        }

        fadeCanvasGroup.alpha = 0f;
        ReleaseInput();
    }

    public IEnumerator FadeTransition(float duration, Action onMiddle = null)
    {
        try
        {
            yield return FadeOut(duration);

            // 화면이 완전히 까매졌을 때 콜백 실행
            onMiddle?.Invoke();

            yield return FadeIn(duration);
        }
        finally
        {
            ReleaseInput(); // 콜백에서 예외가 나도 잠긴 채 남지 않게
        }
    }

    private void ReleaseInput()
    {
        inputLease?.Dispose();
        inputLease = null;
    }
}