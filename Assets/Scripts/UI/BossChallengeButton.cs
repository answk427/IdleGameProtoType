using System;
using UnityEngine;

// 보스 도전 버튼의 View(docs/UI_DESIGN.md 5-7). 보일지는 BossChallengePresenter가 정하고, 여기는 눌렸다고 알리기만 한다.
// 씬에 배치된 HUD 위젯이고, UIRoot가 Presenter를 만들어 연결한다.
public class BossChallengeButton : MonoBehaviour, IBossChallengeView
{
    public event Action Clicked;

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    // 버튼의 OnClick()에 인스펙터로 연결돼 있다.
    public void OnClickChallenge() => Clicked?.Invoke();
}
