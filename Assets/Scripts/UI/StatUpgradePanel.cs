using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 하단 탭(스킬/스탯) 클릭 시에만 보여지는 스탯 업그레이드 패널.
// 골드 표시는 PlayerHud가 전담(상시 노출 정보이므로 여기서는 다루지 않음).
// 이 패널은 골드 변경 시 버튼 활성/비용 텍스트만 갱신.
// 슬롯을 데이터(List<UpgradeSlot>)로 관리해서, 업그레이드 종류가 늘어나도
// 코드 수정 없이 Inspector에서 슬롯 추가만 하면 되도록 구성.
public class StatUpgradePanel : UIBase
{
    [Serializable]
    public class UpgradeSlot
    {
        public UpgradeStatType statType;
        public Button upgradeButton;
        public TextMeshProUGUI costText;
        public TextMeshProUGUI currentValueText; // 현재 스탯 수치 표시 (선택, 비워두면 무시)
    }

    [SerializeField] private List<UpgradeSlot> slots = new List<UpgradeSlot>();

    private PlayerController player;
    private bool listenersBound;

    private void Awake()
    {
        if (listenersBound) return;
        listenersBound = true;

        foreach (var slot in slots)
        {
            if (slot.upgradeButton == null) continue;
            UpgradeStatType capturedType = slot.statType;
            slot.upgradeButton.onClick.AddListener(() => OnClickUpgrade(capturedType));
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
        }

        if (GameManager.Instance != null)
        {
            GameManager.Instance.Wallet.OnGoldChanged -= OnGoldChanged;
        }

        base.Hide();
    }

    private void OnGoldChanged(int _) => RefreshAll();

    private void RefreshAll()
    {
        if (player == null) return;

        int gold = GameManager.Instance != null ? GameManager.Instance.Wallet.Gold : 0;

        var stats = player.Stats;
        foreach (var slot in slots)
        {
            bool isMaxed = stats.IsUpgradeMaxed(slot.statType);
            int cost = stats.GetNextUpgradeCost(slot.statType);

            if (slot.costText != null)
                slot.costText.text = isMaxed ? "MAX" : cost.ToString();

            if (slot.upgradeButton != null)
                slot.upgradeButton.interactable = !isMaxed && gold >= cost;

            if (slot.currentValueText != null)
                slot.currentValueText.text = stats.GetCurrentStatValue(slot.statType).ToString("0");
        }
    }

    private void OnClickUpgrade(UpgradeStatType type)
    {
        if (player == null) return;
        player.TryUpgrade(type);
        // RefreshAll()은 OnUpgraded/OnGoldChanged 이벤트로 자동 호출됨
    }
}
