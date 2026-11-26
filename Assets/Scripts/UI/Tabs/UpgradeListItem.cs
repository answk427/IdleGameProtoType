using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 탭 업그레이드 목록의 항목 1개 (아이콘 / 이름 / 레벨 / 현재→다음 수치 / 비용+업그레이드 버튼).
// 표시 문자열은 CharacterTabPresenter가 만든다.
public class UpgradeListItem : MonoBehaviour
{
    [SerializeField] private Image iconImage;
    [SerializeField] private TextMeshProUGUI nameText;
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private TextMeshProUGUI valueText;
    [SerializeField] private TextMeshProUGUI costText;
    [SerializeField] private Button upgradeButton;

    public UpgradeStatType StatType { get; private set; }

    public void Bind(UpgradeStatType statType, string displayName, Sprite icon, Action<UpgradeStatType> onUpgradeTapped)
    {
        StatType = statType;

        if (iconImage != null) iconImage.sprite = icon;
        if (nameText != null) nameText.text = displayName;

        if (upgradeButton != null)
        {
            upgradeButton.onClick.RemoveAllListeners();
            upgradeButton.onClick.AddListener(() => onUpgradeTapped?.Invoke(StatType));
        }
    }

    public void Show(UpgradeRowModel row)
    {
        if (levelText != null) levelText.text = row.Level;
        if (valueText != null) valueText.text = row.Value;
        if (costText != null) costText.text = row.Cost;
        if (upgradeButton != null) upgradeButton.interactable = row.CanUpgrade;
    }
}
