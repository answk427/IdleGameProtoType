using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// StatUpgradePanel 목록의 항목 1개 (아이콘 / 이름 / 레벨 / 현재→다음 수치 / 비용+업그레이드 버튼)
public class UpgradeListItem : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button upgradeButton;

    public UpgradeStatType StatType { get; private set; }

    private Action<UpgradeStatType> onClickUpgrade;

    public void Bind(UpgradeStatType statType, string displayName, Sprite icon, Action<UpgradeStatType> onUpgradeClicked)
    {
        StatType = statType;
        onClickUpgrade = onUpgradeClicked;

        if (iconImage != null) iconImage.sprite = icon;
        if (nameText != null) nameText.text = displayName;

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(() => onClickUpgrade?.Invoke(StatType));
        }
    }

    public void Refresh(int upgradeLevel, float currentValue, float nextValue, int cost, bool isMaxed, bool canAfford)
    {
        if (levelText != null) levelText.text = $"Lv.{upgradeLevel + 1}";

        string format = StatType == UpgradeStatType.AttackSpeed ? "0.00" : "0";
        if (valueText != null)
            valueText.text = isMaxed
                ? currentValue.ToString(format)
                : $"{currentValue.ToString(format)} → {nextValue.ToString(format)}";

        if (costText != null)
            costText.text = isMaxed ? "MAX" : cost.ToString();

        if (upgradeButton != null)
            upgradeButton.interactable = !isMaxed && canAfford;
    }
}
