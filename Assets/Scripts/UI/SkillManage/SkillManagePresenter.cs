using System;
using System.Collections.Generic;
using System.Linq;

// 스킬 관리 창의 로직. uGUI, GameManager, GameDatabaseManager를 모른다.
// - 배운 스킬을 탭(선택) → 슬롯을 탭하면 배치. 선택 없이 슬롯을 탭하면 비움.
// - 미학습 스킬은 더블탭으로 학습 (조건은 모델이 판정).
// 슬롯은 캐릭터가 아니라 EquippedSlots 상태로 그린다. 캐릭터가 먼저 갱신돼야 한다는 순서 의존이 없다.
public sealed class SkillManagePresenter : IWindowPresenter
{
    private const int NoSelection = -1;

    private readonly ISkillManageView view;
    private readonly PlayerStats stats;
    private readonly Func<int, float> cooldownRatio;
    private readonly IWindowContext window;

    private readonly List<SkillRowModel> rows = new List<SkillRowModel>();
    private readonly Dictionary<int, SkillEntry> skillsById = new Dictionary<int, SkillEntry>();

    private readonly ReactiveProperty<int> selectedSkill = new ReactiveProperty<int>(NoSelection);
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    public SkillManagePresenter(
        ISkillManageView view,
        PlayerStats stats,
        IReadOnlyList<SkillEntry> skills,
        Func<int, float> cooldownRatio,
        IWindowContext window)
    {
        this.view = view;
        this.stats = stats;
        this.cooldownRatio = cooldownRatio;
        this.window = window;

        foreach (SkillEntry entry in skills)
        {
            if (entry?.data == null) continue;

            rows.Add(new SkillRowModel(entry.data.Id, entry.icon, entry.data.SkillName, $"Lv.{entry.data.RequiredLevel}"));
            skillsById[entry.data.Id] = entry;
        }

        view.SetSkills(rows);
        view.SetSlotCount(stats.EquippedSlots.Value.Count);

        stats.LearnedSkills.Subscribe(RenderLearned).AddTo(disposables);
        stats.EquippedSlots.Subscribe(RenderSlots).AddTo(disposables);
        selectedSkill.Subscribe(view.SetSelectedSkill).AddTo(disposables);

        view.SkillTapped += HandleSkillTapped;
        view.SkillDoubleTapped += HandleSkillDoubleTapped;
        view.SlotTapped += HandleSlotTapped;
        view.CloseTapped += HandleCloseTapped;
    }

    public void Tick()
    {
        int count = stats.EquippedSlots.Value.Count;
        for (int slot = 0; slot < count; slot++) view.SetSlotCooldown(slot, cooldownRatio(slot));
    }

    public void Dispose()
    {
        view.SkillTapped -= HandleSkillTapped;
        view.SkillDoubleTapped -= HandleSkillDoubleTapped;
        view.SlotTapped -= HandleSlotTapped;
        view.CloseTapped -= HandleCloseTapped;

        disposables.Dispose();
    }

    private void RenderLearned(IReadOnlyList<int> learned)
    {
        foreach (SkillRowModel row in rows) view.SetSkillLearned(row.Id, learned.Contains(row.Id));
    }

    private void RenderSlots(IReadOnlyList<int> slots)
    {
        for (int slot = 0; slot < slots.Count; slot++)
        {
            if (skillsById.TryGetValue(slots[slot], out SkillEntry entry)) view.ShowSlotSkill(slot, entry.icon);
            else view.ClearSlot(slot);
        }
    }

    private void HandleSkillTapped(int skillId)
    {
        if (!stats.IsSkillLearned(skillId)) return; // 배우지 않은 스킬은 배치 선택 불가

        // 같은 항목을 다시 탭하면 선택 해제
        selectedSkill.Value = selectedSkill.Value == skillId ? NoSelection : skillId;
    }

    // 실패 이유별 안내는 ToastService(2차)가 생기면 붙인다.
    private void HandleSkillDoubleTapped(int skillId) => stats.LearnSkill(skillId);

    private void HandleSlotTapped(int slot)
    {
        int selected = selectedSkill.Value;
        if (selected == NoSelection)
        {
            stats.UnequipSkill(slot);
            return;
        }

        stats.EquipSkill(selected, slot);
        selectedSkill.Value = NoSelection;
    }

    private void HandleCloseTapped() => window.Close();
}
