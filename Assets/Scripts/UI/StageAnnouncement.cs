using System.Collections;
using TMPro;
using UnityEngine;

// 스테이지 전환(최초 진입 포함) 시 화면 상단에 "Stage N"을 잠깐 띄우고 사라지는 안내문구.
public class StageAnnouncement : UITransient
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

    // 패널이 꺼져 있어도 스테이지 변경 이벤트를 받아야 해서 UIManager 등록 시점에 구독한다.
    public override void Initialize()
    {
        GlobalGameEvents.OnStageChanged += HandleStageChanged;
    }

    protected override void OnDestroy()
    {
        GlobalGameEvents.OnStageChanged -= HandleStageChanged;
    }

    private void HandleStageChanged(int stageNumber)
    {
        SetStageNumber(stageNumber);
        Show();
    }

    protected override void OnShown() => Play();

    // 안내가 떠 있는 중에 다음 스테이지로 바뀌면 처음부터 다시 재생한다.
    protected override void OnReshown() => Play();

    private void Play()
    {
        if (canvasGroup != null) canvasGroup.alpha = 0f;
        if (routine != null) StopCoroutine(routine);
        routine = StartCoroutine(ShowAndFade());
    }

    protected override void OnHidden()
    {
        if (routine != null)
        {
            StopCoroutine(routine);
            routine = null;
        }
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
