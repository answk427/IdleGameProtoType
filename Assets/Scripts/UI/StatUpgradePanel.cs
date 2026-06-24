using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 탭: 레벨/경험치/레벨업 버튼 + 스탯 업그레이드 목록.
// 업그레이드 항목은 upgrades 리스트(데이터) 기반으로 런타임에 생성되므로,
// 업그레이드 가능한 스탯이 늘어나도 코드 수정 없이 리스트 항목만 추가하면 된다.
public class StatUpgradePanel : UIBase
{
    [Serializable]
    public class UpgradeDefinition
    {
        public UpgradeStatType statType;
        public string displayName;
        public Sprite icon;
    }

    [Header("레벨 / 경험치")]
    [SerializeField] private Image characterPortraitImage;
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

    private readonly List<UpgradeListItem> spawnedItems = new List<UpgradeListItem>();
    private PlayerController player;

    private void Awake()
    {
        BuildList();

        if (levelUpButton != null)
        {
            levelUpButton.onClick.AddListener(OnClickLevelUp);
        }
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
            Debug.LogError("[StatUpgradePanel] Player를 찾을 수 없습니다.");
            return;
        }

        player.Stats.OnUpgraded += RefreshAll;
        player.Stats.OnExpChanged += OnExpChanged;
        player.Stats.OnLevelUp += OnLevelUp;

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Wallet.OnGoldChanged += OnGoldChanged;
        }

        RefreshAll();
    }

    public override void Hide()
    {
        if (player != null)
        {
            player.Stats.OnUpgraded -= RefreshAll;
            player.Stats.OnExpChanged -= OnExpChanged;
            player.Stats.OnLevelUp -= OnLevelUp;
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Wallet.OnGoldChanged -= OnGoldChanged;
        }

        base.Hide();
    }

    private void OnGoldChanged(int _) => RefreshAll();
    private void OnExpChanged(int current, int required) => RefreshHeader();
    private void OnLevelUp(int newLevel) => RefreshAll();

    private void BuildList()
    {
        if (listContent == null || listItemPrefab == null) return;

        foreach (var def in upgrades)
        {
            UpgradeListItem item = Instantiate(listItemPrefab, listContent);
            item.Bind(def.statType, def.displayName, def.icon, OnClickUpgrade);
            spawnedItems.Add(item);
        }
    }

    private void RefreshAll()
    {
        if (player == null) return;

        RefreshHeader();

        var stats = player.Stats;
        int gold = GameManager.Instance != null ? GameManager.Instance.Wallet.Gold : 0;

        foreach (var item in spawnedItems)
        {
            UpgradeStatType type = item.StatType;
            bool isMaxed = stats.IsUpgradeMaxed(type);
            int cost = stats.GetNextUpgradeCost(type);
            int upgradeLevel = stats.GetCurrentUpgradeLevel(type);
            float currentValue = stats.GetCurrentStatValue(type);
            float nextValue = isMaxed ? currentValue : stats.GetNextStatValue(type);

            item.Refresh(upgradeLevel, currentValue, nextValue, cost, isMaxed, gold >= cost);
        }
    }

    private void RefreshHeader()
    {
        if (player == null) return;

        var stats = player.Stats;

        if (levelText != null) levelText.text = $"Lv. {stats.Level}";

        if (expFillImage != null)
            expFillImage.fillAmount = stats.RequiredExp > 0 ? Mathf.Min(1f, (float)stats.CurrentExp / stats.RequiredExp) : 0f;

        if (expText != null) expText.text = $"EXP {stats.CurrentExp} / {stats.RequiredExp}";

        if (expPercentText != null)
        {
            float percent = stats.RequiredExp > 0 ? (float)stats.CurrentExp / stats.RequiredExp * 100f : 0f;
            expPercentText.text = $"{percent:0.00}%";
        }

        if (levelUpButton != null) levelUpButton.interactable = stats.CanLevelUp;
        if (levelUpNotification != null) levelUpNotification.SetActive(stats.CanLevelUp);
    }

    private void OnClickUpgrade(UpgradeStatType type)
    {
        if (player == null) return;
        player.TryUpgrade(type);
        // RefreshAll()은 OnUpgraded/OnGoldChanged 이벤트로 자동 호출됨
    }

    private void OnClickLevelUp()
    {
        if (player == null) return;
        player.Stats.TryLevelUp();
        // RefreshAll()은 OnLevelUp 이벤트로 자동 호출됨
    }
}
