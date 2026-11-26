using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 HUD의 View(docs/UI_DESIGN.md 5-7): HP, 경험치, 레벨, 골드, 보스전 남은 시간. 받은 값을 그리기만 한다.
// 씬에 배치된 HUD 위젯이고, UIRoot가 Presenter(PlayerHudPresenter)를 만들어 연결한다.
// 게임이 시작되지 않으면(세이브를 읽지 못함) Presenter가 없으므로 숨긴 채로 남는다(빈 값이 보이지 않게).
public class PlayerHud : MonoBehaviour, IPlayerHudView
{
    [Header("HP")]
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("EXP / Level")]
    [SerializeField] private Image expFillImage;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    [Header("보스전 남은 시간")]
    [SerializeField] private GameObject bossTimerRoot;
    [SerializeField] private TextMeshProUGUI bossTimerText;
    [SerializeField] private Color bossTimerColor = Color.black;
    [SerializeField] private Color bossTimerUrgentColor = new Color(0.85f, 0.1f, 0.1f);

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

    public void SetHp(float ratio, string text)
    {
        if (hpFillImage != null) hpFillImage.fillAmount = ratio;
        if (hpText != null) hpText.text = text;
    }

    public void SetExp(float ratio)
    {
        if (expFillImage != null) expFillImage.fillAmount = ratio;
    }

    public void SetLevel(string text)
    {
        if (levelText != null) levelText.text = text;
    }

    public void SetGold(string text)
    {
        if (goldText != null) goldText.text = text;
    }

    public void SetBossTimer(bool visible, string text, bool urgent)
    {
        if (bossTimerRoot != null && bossTimerRoot.activeSelf != visible) bossTimerRoot.SetActive(visible);
        if (!visible || bossTimerText == null) return;

        bossTimerText.text = text;
        bossTimerText.color = urgent ? bossTimerUrgentColor : bossTimerColor;
    }
}
