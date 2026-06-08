using UnityEngine;

public class BossChallengeButton : MonoBehaviour
{
    // 자기 자신을 켜는 함수
    public void Show()
    {
        gameObject.SetActive(true);
    }
    public void Hide()
    {
        gameObject.SetActive(false);
    }

    // 인스펙터 OnClick()에 연결될 함수
    public void OnClickChallenge()
    {
        gameObject.SetActive(false);
        GameManager.Instance.EnterBossBattle();
    }
}
