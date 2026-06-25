using System.Collections;
using TMPro;
using UnityEngine;

public class StageAnnouncement : UIBase
{
    [SerializeField] private TextMeshProUGUI label;
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float visibleDuration = 1.5f;
    [SerializeField] private float fadeDuration = 0.4f;

    private Coroutine routine;

    public void SetStageNumber(int stageNumber)
    {
        if (label != null) label.text = $"Stage {stageNumber}";
    }

    public override void Show()
    {
        base.Show();

        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowAndFade());
    }

    public override void Hide()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
        base.Hide();
    }

    private IEnumerator ShowAndFade()
    {
        yield return Fade(0f, 1f);
        yield return new WaitForSeconds(visibleDuration);
        yield return Fade(1f, 0f);
        Hide();
    }

    private IEnumerator Fade(float from, float to)
    {
        if (canvasGroup == null) yield break;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = to;
    }
}
