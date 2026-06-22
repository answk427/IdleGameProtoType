using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// 하단 탭바(캐릭터/스킬/장비/동료/모험/상점) 버튼들을 UIManager.ToggleTab과 연결.
// 각 슬롯은 버튼 + 탭 타입 + (선택) 잠금 오버레이로 구성됨.
public class TabBarController : MonoBehaviour
{
    [Serializable]
    public class TabSlot
    {
        public UITabType tabType;
        public Button button;
        public GameObject lockOverlay; // 미해금 상태일 때 표시 (이미지의 자물쇠 아이콘)
        public bool isUnlocked = true;
    }

    [SerializeField] private List<TabSlot> tabSlots = new List<TabSlot>();

    private void Start()
    {
        foreach (var slot in tabSlots)
        {
            if (slot.button == null) continue;

            UITabType capturedType = slot.tabType;
            slot.button.onClick.AddListener(() => OnTabClicked(capturedType));

            if (slot.lockOverlay != null)
            {
                slot.lockOverlay.SetActive(!slot.isUnlocked);
            }
        }
    }

    private void OnTabClicked(UITabType tabType)
    {
        TabSlot slot = tabSlots.Find(s => s.tabType == tabType);
        if (slot != null && !slot.isUnlocked)
        {
            Debug.Log($"[TabBarController] {tabType} 탭은 아직 잠겨있습니다.");
            return;
        }

        UIManager.Instance.ToggleTab(tabType);
    }

    public void SetUnlocked(UITabType tabType, bool unlocked)
    {
        TabSlot slot = tabSlots.Find(s => s.tabType == tabType);
        if (slot == null) return;

        slot.isUnlocked = unlocked;
        if (slot.lockOverlay != null)
        {
            slot.lockOverlay.SetActive(!unlocked);
        }
    }
}
