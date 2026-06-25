using UnityEngine;

public class BossChallengeButton : UIBase
{
    // 인스펙터 OnClick()에 연결될 함수
    public void OnClickChallenge()
    {
        Hide();
        if (GameManager.Instance != null) GameManager.Instance.EnterBossBattle();
    }
}
