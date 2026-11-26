using System;
using System.Collections.Generic;

// 스킬 탭(장착 슬롯 바)의 로직. uGUI, GameManager를 모른다.
// - 슬롯은 캐릭터가 아니라 장착 슬롯 상태(EquippedSlots)로 그린다. 캐릭터가 먼저 갱신돼야 한다는 순서 의존이 없다.
// - 쿨다운은 매 프레임 값이라 Tick에서 읽는다(쿨다운은 저장되지 않는 전투 상태라 캐릭터가 갖는다).
// - 슬롯을 누르면 그 스킬을 쓰라고 요청한다(캐릭터가 다음 결정 시점에 쓴다). 관리 버튼은 스킬 관리 창을 연다.
public sealed class SkillTabPresenter : IWindowPresenter
{
    private readonly ISkillTabView view;
    private readonly PlayerStats stats;
    private readonly Func<int, float> cooldownRatio;
    private readonly Action<int> requestSkillSlot;
    private readonly Action openSkillManage;

    private readonly Dictionary<int, SkillEntry> skillsById = new Dictionary<int, SkillEntry>();
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    public SkillTabPresenter(
        ISkillTabView view,
        PlayerStats stats,
        IReadOnlyList<SkillEntry> skills,
        Func<int, float> cooldownRatio,
        Action<int> requestSkillSlot,
        Action openSkillManage)
    {
        this.view = view;
        this.stats = stats;
        this.cooldownRatio = cooldownRatio;
        this.requestSkillSlot = requestSkillSlot;
        this.openSkillManage = openSkillManage;

        foreach (SkillEntry entry in skills)
        {
            if (entry?.data != null) skillsById[entry.data.Id] = entry;
        }

        view.SetSlotCount(stats.EquippedSlots.Value.Count);
        stats.EquippedSlots.Subscribe(RenderSlots).AddTo(disposables);
        stats.HasLearnableSkill.Subscribe(view.SetManageBadge).AddTo(disposables); // 레드닷: 관리 창에 배울 스킬이 있다

        view.SlotTapped += HandleSlotTapped;
        view.ManageTapped += HandleManageTapped;
    }

    public void Tick()
    {
        int count = stats.EquippedSlots.Value.Count;
        for (int slot = 0; slot < count; slot++) view.SetSlotCooldown(slot, cooldownRatio(slot));
    }

    public void Dispose()
    {
        view.SlotTapped -= HandleSlotTapped;
        view.ManageTapped -= HandleManageTapped;
        disposables.Dispose();
    }

    private void RenderSlots(IReadOnlyList<int> slots)
    {
        for (int slot = 0; slot < slots.Count; slot++)
        {
            if (skillsById.TryGetValue(slots[slot], out SkillEntry entry)) view.ShowSlotSkill(slot, entry.icon);
            else view.ClearSlot(slot);
        }
    }

    private void HandleSlotTapped(int slot) => requestSkillSlot(slot);

    private void HandleManageTapped() => openSkillManage();
}
