using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 캐릭터 탭: 레벨/경험치/레벨업 버튼 + 스탯 업그레이드 목록.
// 업그레이드 항목은 upgrades 리스트(데이터) 기반으로 런타임에 생성되므로,
// 업그레이드 가능한 스탯이 늘어나도 코드 수정 없이 리스트 항목만 추가하면 된다.
public class StatUpgradePanel : UITabPanel
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
    private CompositeDisposable subscriptions;

    private void Awake()
    {
        BuildList();

        if (levelUpButton != null)
        {
            levelUpButton.onClick.AddListener(OnClickLevelUp);
        }
    }

    protected override void OnShown()
    {
        if (player == null && GameManager.Instance != null)
        {
            player = GameManager.Instance.GetPlayer();
        }

        if (player == null)
        {
            Debug.LogError("[StatUpgradePanel] Player를 찾을 수 없습니다.");
            return;
        }

        // 진행도의 상태를 구독한다. 구독하는 순간 지금 값으로 한 번씩 그리고, 진행도 교체(Apply) 뒤에도 따라온다.
        // 창 이관(다음 작업 6)에서 Presenter로 옮긴다.
        subscriptions?.Dispose();
        subscriptions = new CompositeDisposable();
        PlayerStats stats = player.Stats;
        stats.Level.Subscribe(_ => RefreshAll()).AddTo(subscriptions);
        stats.Exp.Subscribe(_ => RefreshHeader()).AddTo(subscriptions);
        stats.CanLevelUp.Subscribe(_ => RefreshHeader()).AddTo(subscriptions);
        foreach (UpgradeListItem item in spawnedItems)
            stats.UpgradeLevel(item.StatType).Subscribe(_ => RefreshAll()).AddTo(subscriptions);
        if (GameManager.Instance != null)
            GameManager.Instance.Wallet.Gold.Subscribe(_ => RefreshAll()).AddTo(subscriptions);
    }

    protected override void OnHidden()
    {
        subscriptions?.Dispose();
        subscriptions = null;
    }

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
        GameNumber gold = GameManager.Instance != null ? GameManager.Instance.Wallet.Gold.Value : GameNumber.Zero;

        foreach (var item in spawnedItems)
        {
            UpgradeStatType type = item.StatType;
            bool isMaxed = stats.IsUpgradeMaxed(type);
            GameNumber cost = stats.GetNextUpgradeCost(type);
            int upgradeLevel = stats.UpgradeLevel(type).Value;
            float currentValue = stats.GetCurrentStatValue(type);
            float nextValue = isMaxed ? currentValue : stats.GetNextStatValue(type);

            item.Refresh(upgradeLevel, currentValue, nextValue, cost, isMaxed, gold >= cost);
        }
    }

    private void RefreshHeader()
    {
        if (player == null) return;

        var stats = player.Stats;
        int exp = stats.Exp.Value;
        int required = stats.RequiredExp.Value;
        bool canLevelUp = stats.CanLevelUp.Value;

        if (levelText != null) levelText.text = $"Lv. {stats.Level.Value}";

        if (expFillImage != null)
            expFillImage.fillAmount = required > 0 ? Mathf.Min(1f, (float)exp / required) : 0f;

        if (expText != null) expText.text = $"EXP {NumberFormatUtility.Format(exp)} / {NumberFormatUtility.Format(required)}";

        if (expPercentText != null)
        {
            float percent = required > 0 ? (float)exp / required * 100f : 0f;
            expPercentText.text = $"{percent:0.00}%";
        }

        if (levelUpButton != null) levelUpButton.interactable = canLevelUp;
        if (levelUpNotification != null) levelUpNotification.SetActive(canLevelUp);
    }

    private void OnClickUpgrade(UpgradeStatType type)
    {
        if (player == null || GameManager.Instance == null) return;
        // 결과(UpgradeResult)별 안내는 1차 UI 이관 때 붙인다. 지금은 버튼 활성화로 이미 걸러진다.
        GameManager.Instance.Session.TryUpgrade(type);
        // 화면은 업그레이드 단계·골드 상태를 구독해 따라온다
    }

    private void OnClickLevelUp()
    {
        if (player == null) return;
        player.Stats.TryLevelUp();
        // 화면은 레벨·경험치 상태를 구독해 따라온다
    }
}
