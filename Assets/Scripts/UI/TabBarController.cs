using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 하단 탭바 버튼들을 tabs 리스트(데이터) 기반으로 런타임에 생성해서 UIManager.ToggleTab과 연결.
// 탭 추가/삭제는 tabs 리스트 항목만 늘리거나 줄이면 됨 (씬 하이어라키 편집 불필요).
// 버튼 개수에 따른 균등 배치는 tabContainer에 붙은 HorizontalLayoutGroup이 처리.
public class TabBarController : MonoBehaviour
{
    [Serializable]
    public class TabDefinition
    {
        public UITabType tabType;
        public string label;
        public Sprite icon;
        public bool isUnlockedByDefault = true;
    }

    private class RuntimeTabSlot
    {
        public UITabType tabType;
        public Button button;
        public GameObject lockOverlay;
        public bool isUnlocked;
    }

    [SerializeField] private GameObject tabButtonPrefab;
    [SerializeField] private Transform tabContainer;
    [SerializeField] private List<TabDefinition> tabs = new List<TabDefinition>();

    private readonly List<RuntimeTabSlot> runtimeSlots = new List<RuntimeTabSlot>();

    private void Start()
    {
        foreach (var def in tabs)
        {
            GameObject instance = Instantiate(tabButtonPrefab, tabContainer);

            TextMeshProUGUI label = instance.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = def.label;

            Image icon = instance.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && def.icon != null) icon.sprite = def.icon;

            GameObject lockOverlay = instance.transform.Find("LockOverlay")?.gameObject;
            if (lockOverlay != null) lockOverlay.SetActive(!def.isUnlockedByDefault);

            Button button = instance.GetComponent<Button>();
            UITabType capturedType = def.tabType;
            button.onClick.AddListener(() => OnTabClicked(capturedType));

            runtimeSlots.Add(new RuntimeTabSlot
            {
                tabType = capturedType,
                button = button,
                lockOverlay = lockOverlay,
                isUnlocked = def.isUnlockedByDefault
            });
        }
    }

    private void OnTabClicked(UITabType tabType)
    {
        RuntimeTabSlot slot = runtimeSlots.Find(s => s.tabType == tabType);
        if (slot != null && !slot.isUnlocked)
        {
            Debug.Log($"[TabBarController] {tabType} 탭은 아직 잠겨있습니다.");
            return;
        }

        if (UIManager.Instance != null) UIManager.Instance.ToggleTab(tabType);
    }

    public void SetUnlocked(UITabType tabType, bool unlocked)
    {
        RuntimeTabSlot slot = runtimeSlots.Find(s => s.tabType == tabType);
        if (slot == null) return;

        slot.isUnlocked = unlocked;
        if (slot.lockOverlay != null)
        {
            slot.lockOverlay.SetActive(!unlocked);
        }
    }
}
