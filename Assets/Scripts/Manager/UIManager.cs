using System.Collections;
using UnityEngine;
using UnityEngine.UI; // UI 사용을 위해 필수

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    [Header("UI Panels")]
    [SerializeField] private BossChallengeButton bossChallengeButton;

    [Header("Common Effects")]
    [SerializeField] private CanvasGroup fadeCanvasGroup; //FadeOut, FadeIn용 CanvasGroup

    private void Awake()
    {
        Instance = this;

        if (bossChallengeButton != null)
        {
            bossChallengeButton.Hide();
        }

        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
            fadeCanvasGroup.blocksRaycasts = false; // 뒤에 버튼 클릭 가능하게
        }
    }

    public void ShowBossChallengeButton()
    {
        if (bossChallengeButton != null)
        {
            bossChallengeButton.Show();
        }
    }

    public IEnumerator FadeOut(float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        // 터치 차단
        fadeCanvasGroup.blocksRaycasts = true;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            // 시간에 따라 0에서 1로 부드럽게 증가
            fadeCanvasGroup.alpha = Mathf.Clamp01(elapsedTime / duration);

            yield return null; // 다음 프레임까지 대기
        }

        fadeCanvasGroup.alpha = 1f; // 확실하게 1로 고정
    }

    public IEnumerator FadeIn(float duration)
    {
        if (fadeCanvasGroup == null) yield break;

        float elapsedTime = 0f;
        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;

            // 시간에 따라 1에서 0으로 부드럽게 감소
            fadeCanvasGroup.alpha = Mathf.Clamp01(1f - (elapsedTime / duration));

            yield return null;
        }

        fadeCanvasGroup.alpha = 0f; // 확실하게 0으로 고정

        // 터치 허용
        fadeCanvasGroup.blocksRaycasts = false;
    }
}