using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 플레이어 체력/경험치/레벨/골드만 보여주는 상시 HUD.
public class PlayerHud : UIHud
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

    protected override void OnShown()
    {
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
        player.Stats.OnUpgraded += RefreshHpBar;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Wallet.OnGoldChanged += UpdateGoldText;
        }

        UpdateHpBar(player.CurrentHp, player.MaxHp);
        UpdateExpBar(player.Stats.CurrentExp, player.Stats.RequiredExp);
        UpdateLevelText(player.Stats.Level);
        UpdateGoldText(GameManager.Instance != null ? GameManager.Instance.Wallet.Gold : GameNumber.Zero);
    }

    protected override void OnHidden()
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
            GameManager.Instance.Wallet.OnGoldChanged -= UpdateGoldText;
        }
    }

    private void RefreshHpBar() => UpdateHpBar(player.CurrentHp, player.MaxHp);

    private void UpdateHpBar(GameNumber currentHp, GameNumber maxHp)
    {
        if (hpFillImage != null && maxHp > GameNumber.Zero) hpFillImage.fillAmount = (float)(currentHp.ToDouble() / maxHp.ToDouble());
        if (hpText != null) hpText.text = $"{NumberFormatUtility.Format(currentHp)} / {NumberFormatUtility.Format(maxHp)}";
    }

    private void UpdateExpBar(int currentExp, int requiredExp)
    {
        // 레벨업이 버튼으로 수동 처리되면서 currentExp가 requiredExp를 넘어선 채로 머무를 수 있어 클램프.
        Debug.Log($"UpdateExpBar, currentExp:{currentExp}, requiredExp:{requiredExp}");
        if (expFillImage != null && requiredExp > 0)
        {
            expFillImage.fillAmount = Mathf.Min(1f, (float)currentExp / requiredExp);
            Debug.Log("UpdateExpBar in if");
        }
    }

    private void UpdateLevelText(int level)
    {
        if (levelText != null) levelText.text = $"Lv.{level}";
    }

    private void UpdateGoldText(GameNumber gold)
    {
        if (goldText != null) goldText.text = NumberFormatUtility.Format(gold);
    }
}
