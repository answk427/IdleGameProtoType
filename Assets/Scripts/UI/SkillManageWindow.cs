using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public sealed class SkillManageArgs : IWindowArgs<SkillManageWindow> { }

// 스킬 관리 창의 View. 표시와 입력 전달만 하고, 무엇을 할지는 SkillManagePresenter가 정한다.
// 더블탭 "판정"은 제스처라 View가 갖는다.
public class SkillManageWindow : UIWindow<SkillManageArgs>, ISkillManageView
{
    // 모바일 터치는 EventSystem이 clickCount를 1→2로 못 올려주는 경우가 많아서
    // (PC에서는 더블클릭으로 인식되는데 모바일에서는 항상 1), 직접 시간차로 판정한다.
    private const float DoubleTapSeconds = 0.3f;

    [SerializeField] private Transform slotContainer;
    [SerializeField] private SkillSlotWidget slotWidgetPrefab;
    [SerializeField] private Transform listContent;       // ScrollView Content
    [SerializeField] private SkillListItem listItemPrefab;
    [SerializeField] private UIButton closeButton;

    private readonly List<SkillSlotWidget> slotWidgets = new List<SkillSlotWidget>();
    private readonly List<SkillListItem> items = new List<SkillListItem>();
    private int visibleItemCount;

    // 직전에 누른 "항목"까지 봐야 해서(A→B→A는 더블탭이 아님) 항목이 아니라 창이 기억한다.
    private int lastTappedSkillId;
    private float lastTapTime;
    private bool hasLastTap;

    public override WindowOptions Options => WindowOptions.ModalPopup;

    public event Action<int> SkillTapped;
    public event Action<int> SkillDoubleTapped;
    public event Action<int> SlotTapped;
    public event Action CloseTapped;

    protected override void OnInitialize()
    {
        if (closeButton != null) closeButton.onClick.AddListener(() => CloseTapped?.Invoke());
    }

    protected override void OnOpening()
    {
        hasLastTap = false;
    }

    // 행은 다시 만들지 않고 값만 갈아끼운다.
    public void SetSkills(IReadOnlyList<SkillRowModel> rows)
    {
        while (items.Count < rows.Count) items.Add(Instantiate(listItemPrefab, listContent));

        visibleItemCount = rows.Count;
        for (int i = 0; i < items.Count; i++)
        {
            bool visible = i < visibleItemCount;
            items[i].gameObject.SetActive(visible);
            if (!visible) continue;

            SkillRowModel row = rows[i];
            items[i].Bind(row.Id, row.Icon, row.Name, row.Requirement, HandleItemTapped);
        }
    }

    public void SetSkillLearned(int skillId, bool learned)
    {
        for (int i = 0; i < visibleItemCount; i++)
        {
            if (items[i].SkillId == skillId) items[i].SetLearned(learned);
        }
    }

    public void SetSkillLearnable(int skillId, bool learnable)
    {
        for (int i = 0; i < visibleItemCount; i++)
        {
            if (items[i].SkillId == skillId) items[i].SetLearnable(learnable);
        }
    }

    public void SetSelectedSkill(int skillId)
    {
        for (int i = 0; i < visibleItemCount; i++) items[i].SetSelected(items[i].SkillId == skillId);
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

    private void HandleItemTapped(int skillId)
    {
        float now = Time.unscaledTime;
        bool isDoubleTap = hasLastTap && skillId == lastTappedSkillId && now - lastTapTime <= DoubleTapSeconds;

        // 더블탭 뒤의 세 번째 탭은 다시 단일 탭이다.
        hasLastTap = !isDoubleTap;
        lastTappedSkillId = skillId;
        lastTapTime = now;

        if (isDoubleTap) SkillDoubleTapped?.Invoke(skillId);
        else SkillTapped?.Invoke(skillId);
    }
}
