using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 체력/경험치/골드/업그레이드를 보여주는 상시 HUD.
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

    [Header("업그레이드 버튼")]
    [SerializeField] private Button hpUpgradeButton;
    [SerializeField] private TextMeshProUGUI hpUpgradeCostText;
    [SerializeField] private Button attackUpgradeButton;
    [SerializeField] private TextMeshProUGUI attackUpgradeCostText;
    [SerializeField] private Button speedUpgradeButton;
    [SerializeField] private TextMeshProUGUI speedUpgradeCostText;

    private PlayerController player;
    private int lastShownGold = -1;

    private void Awake()
    {
        if (hpUpgradeButton != null) hpUpgradeButton.onClick.AddListener(OnClickHpUpgrade);
        if (attackUpgradeButton != null) attackUpgradeButton.onClick.AddListener(OnClickAttackUpgrade);
        if (speedUpgradeButton != null) speedUpgradeButton.onClick.AddListener(OnClickSpeedUpgrade);
    }

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
        player.Stats.OnUpgraded += RefreshGoldAndButtons;

        UpdateHpBar(player.CurrentHp, player.MaxHp);
        UpdateExpBar(player.Stats.CurrentExp, player.Stats.RequiredExp);
        UpdateLevelText(player.Stats.Level);
        RefreshGoldAndButtons();
    }

    public override void Hide()
    {
        if (player != null)
        {
            player.OnHpChanged -= UpdateHpBar;
            player.Stats.OnExpChanged -= UpdateExpBar;
            player.Stats.OnLevelUp -= UpdateLevelText;
            player.Stats.OnUpgraded -= RefreshGoldAndButtons;
        }

        base.Hide();
    }

    private void Update()
    {
        // 골드 변경 이벤트가 없어서, 변경됐을 때만 가볍게 갱신
        if (GameManager.Instance == null) return;
        if (GameManager.Instance.Gold != lastShownGold)
        {
            RefreshGoldAndButtons();
        }
    }

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

    private void RefreshGoldAndButtons()
    {
        if (player == null) return;

        int gold = GameManager.Instance != null ? GameManager.Instance.Gold : 0;
        lastShownGold = gold;
        if (goldText != null) goldText.text = $"Gold: {gold}";

        var stats = player.Stats;
        SetUpgradeButton(hpUpgradeButton, hpUpgradeCostText, stats.IsHpUpgradeMaxed, stats.NextHpUpgradeCost, gold);
        SetUpgradeButton(attackUpgradeButton, attackUpgradeCostText, stats.IsAttackUpgradeMaxed, stats.NextAttackUpgradeCost, gold);
        SetUpgradeButton(speedUpgradeButton, speedUpgradeCostText, stats.IsSpeedUpgradeMaxed, stats.NextSpeedUpgradeCost, gold);
    }

    private void SetUpgradeButton(Button button, TextMeshProUGUI costText, bool isMaxed, int cost, int gold)
    {
        if (isMaxed)
        {
            if (costText != null) costText.text = "MAX";
            if (button != null) button.interactable = false;
        }
        else
        {
            if (costText != null) costText.text = cost.ToString();
            if (button != null) button.interactable = gold >= cost;
        }
    }

    private void OnClickHpUpgrade()
    {
        if (player == null) return;
        player.TryUpgradeHp();
        RefreshGoldAndButtons();
    }

    private void OnClickAttackUpgrade()
    {
        if (player == null) return;
        player.TryUpgradeAttack();
        RefreshGoldAndButtons();
    }

    private void OnClickSpeedUpgrade()
    {
        if (player == null) return;
        player.TryUpgradeSpeed();
        RefreshGoldAndButtons();
    }
}
