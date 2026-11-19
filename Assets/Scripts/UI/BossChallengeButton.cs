using UnityEngine;

public class BossChallengeButton : UIHud
{
    // 게임 시작과 함께 뜨지 않고, 보스 도전이 가능해졌을 때만 뜬다.
    protected override bool ShowOnGameStart => false;

    public override void Initialize()
    {
        base.Initialize();
        GlobalGameEvents.OnBossChallengeAvailable += Show;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
        GlobalGameEvents.OnBossChallengeAvailable -= Show;
    }

    // 인스펙터 OnClick()에 연결될 함수
    public void OnClickChallenge()
    {
        Hide();
        if (GameManager.Instance != null) GameManager.Instance.EnterBossBattle();
    }
}
