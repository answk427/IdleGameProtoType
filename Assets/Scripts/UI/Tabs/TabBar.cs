using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// 하단 탭바의 View(씬에 배치된 HUD 위젯). 버튼을 tabs 목록(종류, 글, 아이콘)으로 만들고, 누르면 알리기만 한다.
// 탭 추가/삭제는 tabs 목록 항목만 늘리거나 줄이면 된다. 균등 배치는 tabContainer의 HorizontalLayoutGroup이 한다.
// 어느 탭이 열리는지, 잠겼는지, 무엇이 선택됐는지는 TabBarPresenter가 정한다.
// Presenter가 없으면(시작 실패) 모든 탭이 잠긴 모양이고 눌러도 아무 일이 없다.
public class TabBar : MonoBehaviour, ITabBarView
{
    [Serializable]
    public class TabDefinition
    {
        public UITabType tabType;
        public string label;
        public Sprite icon;
    }

    private sealed class Slot
    {
        public UITabType Type;
        public Image Background;
        public Color NormalColor;
        public GameObject LockOverlay;
    }

    [SerializeField] private GameObject tabButtonPrefab;
    [SerializeField] private Transform tabContainer;
    [SerializeField] private List<TabDefinition> tabs = new List<TabDefinition>();

    [Header("연출")]
    [Tooltip("선택된 탭 버튼의 배경색")]
    [SerializeField] private Color selectedColor = new Color(1f, 0.86f, 0.45f, 1f);

    private readonly List<Slot> slots = new List<Slot>();
    private readonly List<UITabType> tabTypes = new List<UITabType>();

    public event Action<UITabType> TabTapped;

    public IReadOnlyList<UITabType> Tabs => tabTypes;

    // Presenter는 GameInitializer.Start(다른 Start보다 먼저)에서 생기므로 버튼은 Awake에서 만든다.
    private void Awake()
    {
        foreach (TabDefinition def in tabs)
        {
            if (tabTypes.Contains(def.tabType))
            {
                Debug.LogError($"[TabBar] 탭이 두 번 있습니다: {def.tabType}");
                continue;
            }

            GameObject instance = Instantiate(tabButtonPrefab, tabContainer);

            TextMeshProUGUI label = instance.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null) label.text = def.label;

            Image icon = instance.transform.Find("Icon")?.GetComponent<Image>();
            if (icon != null && def.icon != null) icon.sprite = def.icon;

            var slot = new Slot
            {
                Type = def.tabType,
                Background = instance.GetComponent<Image>(),
                LockOverlay = instance.transform.Find("LockOverlay")?.gameObject,
            };
            slot.NormalColor = slot.Background != null ? slot.Background.color : Color.white;
            if (slot.LockOverlay != null) slot.LockOverlay.SetActive(true);

            UITabType type = def.tabType;
            instance.GetComponent<Button>().onClick.AddListener(() => TabTapped?.Invoke(type));

            slots.Add(slot);
            tabTypes.Add(type);
        }
    }

    public void SetLocked(UITabType tab, bool locked)
    {
        Slot slot = Find(tab);
        if (slot?.LockOverlay != null) slot.LockOverlay.SetActive(locked);
    }

    public void SetSelected(UITabType tab)
    {
        foreach (Slot slot in slots)
        {
            if (slot.Background != null) slot.Background.color = slot.Type == tab ? selectedColor : slot.NormalColor;
        }
    }

    private Slot Find(UITabType tab)
    {
        foreach (Slot slot in slots)
        {
            if (slot.Type == tab) return slot;
        }
        return null;
    }
}
