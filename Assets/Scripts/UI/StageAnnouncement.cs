using System.Collections;
using TMPro;
using UnityEngine;

// 스테이지 전환(최초 진입 포함) 시 화면 상단에 "Stage N"을 잠깐 띄우고 사라지는 안내문구.
// 다른 패널들과 동일하게 UIBase를 상속해서 UIManager가 생명주기(시작 시 비활성화 등)를
// 관리하게 한다. Hide()가 SetActive(false)를 하므로, 안 보이는 동안은 레이캐스트도
// 절대 막지 않는다 (alpha만 0으로 두는 방식은 안 보여도 클릭을 가로채는 문제가 있었음).
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
