using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillTabArgs : IWindowArgs<SkillTabWindow> { }

// 스킬 탭의 View: 장착 슬롯(아이콘, 쿨다운)과 스킬 관리 창 버튼. 무엇을 할지는 SkillTabPresenter가 정한다.
// 프리팹 루트는 화면 전체, 내용은 HUD의 탭 내용 영역(하단 UI) 위에 놓인다.
public class SkillTabWindow : UIWindow<SkillTabArgs>, ISkillTabView
{
    [SerializeField] private Transform slotContainer;
    [SerializeField] private SkillSlotWidget slotWidgetPrefab;
    [SerializeField] private UIButton settingsButton;
    [Tooltip("스킬 설정 버튼의 레드닷")]
    [SerializeField] private GameObject settingsBadge;

    private readonly List<SkillSlotWidget> slotWidgets = new List<SkillSlotWidget>();

    public override WindowOptions Options => WindowOptions.Tab;

    public event Action<int> SlotTapped;
    public event Action ManageTapped;

    protected override void OnInitialize()
    {
        if (settingsButton != null) settingsButton.onClick.AddListener(() => ManageTapped?.Invoke());
    }

    public void SetSlotCount(int count)
    {
        while (slotWidgets.Count < count)
        {
            SkillSlotWidget widget = Instantiate(slotWidgetPrefab, slotContainer);
            widget.Initialize(slotWidgets.Count, slot => SlotTapped?.Invoke(slot));
            slotWidgets.Add(widget);
        }

        for (int i = 0; i < slotWidgets.Count; i++) slotWidgets[i].gameObject.SetActive(i < count);
    }

    public void ShowSlotSkill(int slot, Sprite icon) => slotWidgets[slot].SetIcon(icon);

    public void ClearSlot(int slot) => slotWidgets[slot].SetEmpty();

    public void SetSlotCooldown(int slot, float ratio) => slotWidgets[slot].SetCooldown(ratio);

    public void SetManageBadge(bool visible)
    {
        if (settingsBadge != null) settingsBadge.SetActive(visible);
    }
}
