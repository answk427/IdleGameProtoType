using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class CharacterTabArgs : IWindowArgs<CharacterTabWindow> { }

// 캐릭터 탭의 View: 레벨/경험치/레벨업 버튼 + 스탯 업그레이드 목록. 무엇을 보일지는 CharacterTabPresenter가 정한다.
// 업그레이드 항목은 upgrades 목록(연출 데이터)으로 만들어서, 업그레이드 가능한 스탯을 목록에 더하면 줄이 생긴다.
// 프리팹 루트는 화면 전체, 내용은 HUD의 탭 내용 영역(하단 UI) 위에 놓인다.
public class CharacterTabWindow : UIWindow<CharacterTabArgs>, ICharacterTabView
{
    [Serializable]
    public class UpgradeDefinition
    {
        public UpgradeStatType statType;
        public string displayName;
        public Sprite icon;
    }

    [Header("레벨 / 경험치")]
    [SerializeField] private TextMeshProUGUI levelText;
    [SerializeField] private Image expFillImage;
    [SerializeField] private TextMeshProUGUI expText;
    [SerializeField] private TextMeshProUGUI expPercentText;
    [SerializeField] private Button levelUpButton;
    [SerializeField] private GameObject levelUpNotification; // 레벨업 가능할 때만 표시되는 알림 점

    [Header("업그레이드 목록")]
    [SerializeField] private Transform listContent;
    [SerializeField] private UpgradeListItem listItemPrefab;
    [SerializeField] private List<UpgradeDefinition> upgrades = new List<UpgradeDefinition>();

    private readonly Dictionary<UpgradeStatType, UpgradeListItem> items = new Dictionary<UpgradeStatType, UpgradeListItem>();
    private readonly List<UpgradeStatType> rows = new List<UpgradeStatType>();

    public override WindowOptions Options => WindowOptions.Tab;

    public event Action LevelUpTapped;
    public event Action<UpgradeStatType> UpgradeTapped;

    public IReadOnlyList<UpgradeStatType> UpgradeRows => rows;

    protected override void OnInitialize()
    {
        if (levelUpButton != null) levelUpButton.onClick.AddListener(() => LevelUpTapped?.Invoke());

        foreach (UpgradeDefinition def in upgrades)
        {
            if (items.ContainsKey(def.statType))
            {
                Debug.LogError($"[CharacterTabWindow] 업그레이드 줄이 두 번 있습니다: {def.statType}");
                continue;
            }

            UpgradeListItem item = Instantiate(listItemPrefab, listContent);
            item.Bind(def.statType, def.displayName, def.icon, type => UpgradeTapped?.Invoke(type));
            items.Add(def.statType, item);
            rows.Add(def.statType);
        }
    }

    public void SetLevel(string text)
    {
        if (levelText != null) levelText.text = text;
    }

    public void SetExp(float fillRatio, string text, string percentText)
    {
        if (expFillImage != null) expFillImage.fillAmount = fillRatio;
        if (expText != null) expText.text = text;
        if (expPercentText != null) expPercentText.text = percentText;
    }

    public void SetLevelUpAvailable(bool available)
    {
        if (levelUpButton != null) levelUpButton.interactable = available;
        if (levelUpNotification != null) levelUpNotification.SetActive(available);
    }

    public void SetUpgradeRow(UpgradeStatType type, UpgradeRowModel row)
    {
        if (items.TryGetValue(type, out UpgradeListItem item)) item.Show(row);
    }
}
