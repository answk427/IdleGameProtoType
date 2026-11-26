using System;
using UnityEngine;

// 보스 도전 버튼의 View(docs/UI_DESIGN.md 5-7). 보일지는 BossChallengePresenter가 정하고, 여기는 눌렸다고 알리기만 한다.
// 씬에 배치된 HUD 위젯이고, UIRoot가 Presenter를 만들어 연결한다.
// 게임이 시작되지 않으면(세이브를 읽지 못함) Presenter가 없으므로 숨긴 채로 남는다(PlayerHud와 같은 규칙).
public class BossChallengeButton : MonoBehaviour, IBossChallengeView
{
    public event Action Clicked;

    private bool shownByPresenter;

    private void Awake()
    {
        // Presenter가 켜기 전에는 숨긴다(씬에 꺼진 채 저장돼 있어도, 켜질 때의 Awake가 다시 끄지 않게 표시를 본다).
        if (!shownByPresenter) gameObject.SetActive(false);
    }

    public void SetVisible(bool visible)
    {
        shownByPresenter = visible;
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    // 버튼의 OnClick()에 인스펙터로 연결돼 있다.
    public void OnClickChallenge() => Clicked?.Invoke();
}
