using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 체력/경험치/레벨/골드만 보여주는 상시 HUD.
// 업그레이드는 StatUpgradePanel(탭 전용)로 분리됨.
// UIManager가 Awake 시점에 자동 등록하므로, GameManager.Start()에서
// UIManager.Instance.ShowUI<PlayerHud>()를 호출해서 켜준다.
public class PlayerHud : UIBase
{
    [Header("HP")]
    [SerializeField] private Image hpFillImage;
    [SerializeField] private TextMeshProUGUI hpText;

    [Header("EXP / Level")]
    [SerializeField] private Image expFillImage;
    [SerializeField] private TextMeshProUGUI levelText;

    [Header("Gold")]
    [SerializeField] private TextMeshProUGUI goldText;

    private PlayerController player;

    public override void Show()
    {
        base.Show();

        if (player == null && GameManager.Instance != null)
        {
            player = GameManager.Instance.GetPlayer();
        }

        if (player == null)
        {
            Debug.LogError("[PlayerHud] Player를 찾을 수 없습니다.");
            return;
        }

        player.OnHpChanged += UpdateHpBar;
        player.Stats.OnExpChanged += UpdateExpBar;
        player.Stats.OnLevelUp += UpdateLevelText;
        player.Stats.OnUpgraded += RefreshHpBar; // HP 업그레이드는 MaxHp만 바꾸고 OnHpChanged를 안 쏘므로 별도로 구독

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged += UpdateGoldText;
        }

        UpdateHpBar(player.CurrentHp, player.MaxHp);
        UpdateExpBar(player.Stats.CurrentExp, player.Stats.RequiredExp);
        UpdateLevelText(player.Stats.Level);
        UpdateGoldText(GameManager.Instance != null ? GameManager.Instance.Gold : 0);
    }

    public override void Hide()
    {
        if (player != null)
        {
            player.OnHpChanged -= UpdateHpBar;
            player.Stats.OnExpChanged -= UpdateExpBar;
            player.Stats.OnLevelUp -= UpdateLevelText;
            player.Stats.OnUpgraded -= RefreshHpBar;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGoldChanged -= UpdateGoldText;
        }

        base.Hide();
    }

    private void RefreshHpBar() => UpdateHpBar(player.CurrentHp, player.MaxHp);

    private void UpdateHpBar(float currentHp, float maxHp)
    {
        if (hpFillImage != null && maxHp > 0) hpFillImage.fillAmount = currentHp / maxHp;
        if (hpText != null) hpText.text = $"{Mathf.CeilToInt(currentHp)} / {Mathf.CeilToInt(maxHp)}";
    }

    private void UpdateExpBar(int currentExp, int requiredExp)
    {
        if (expFillImage != null && requiredExp > 0) expFillImage.fillAmount = (float)currentExp / requiredExp;
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null) levelText.text = $"Lv.{level}";
    }

    private void UpdateGoldText(int gold)
    {
        if (goldText != null) goldText.text = $"Gold: {gold}";
    }
}
