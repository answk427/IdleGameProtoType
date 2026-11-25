using System.Collections;
using TMPro;
using UnityEngine;

// 스테이지 안내("Stage N")의 View(docs/UI_DESIGN.md 5-7). 언제, 무슨 글을 띄울지는 StageAnnouncementPresenter가 정한다.
// 애니메이션은 화면 시간(unscaledDeltaTime)이라 배속·일시정지와 무관하게 같은 속도로 보인다(docs/UI_DESIGN.md 6-5).
public class StageAnnouncement : MonoBehaviour, IStageAnnouncementView
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float visibleDuration = 1.5f;
    [SerializeField] private float fadeDuration = 0.4f;

    private Coroutine routine;

    public void Play(string text)
    {
        if (label != null) label.text = text;
        if (canvasGroup != null) canvasGroup.alpha = 0f;

        gameObject.SetActive(true);
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowAndFade());
    }

    public void HideNow()
    {
        if (routine != null) StopCoroutine(routine);
        routine = null;
        gameObject.SetActive(false);
    }

    // 꺼지면 코루틴도 멈춘다.
    private void OnDisable() => routine = null;

    private IEnumerator ShowAndFade()
    {
        yield return Fade(0f, 1f);

        for (float shown = 0f; shown < visibleDuration; shown += Time.unscaledDeltaTime) yield return null;

        yield return Fade(1f, 0f);
        routine = null;
        gameObject.SetActive(false);
    }

    private IEnumerator Fade(float from, float to)
    {
        if (canvasGroup == null) yield break;

        for (float t = 0f; t < fadeDuration; t += Time.unscaledDeltaTime)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
