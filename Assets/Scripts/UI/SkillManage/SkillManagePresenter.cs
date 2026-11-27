using System;
using System.Collections.Generic;
using System.Linq;

// 스킬 관리 창의 로직. uGUI를 모른다(데이터는 주입받은 스킬 표만 안다).
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
    private readonly IToastService toasts;

    private readonly List<SkillRowModel> rows = new List<SkillRowModel>();
    private readonly Dictionary<int, SkillEntry> skillsById = new Dictionary<int, SkillEntry>();

    private readonly ReactiveProperty<int> selectedSkill = new ReactiveProperty<int>(NoSelection);
    private readonly CompositeDisposable disposables = new CompositeDisposable();

    public SkillManagePresenter(
        ISkillManageView view,
        PlayerStats stats,
        IReadOnlyList<SkillEntry> skills,
        Func<int, float> cooldownRatio,
        IWindowContext window,
        IToastService toasts)
    {
        this.view = view;
        this.stats = stats;
        this.cooldownRatio = cooldownRatio;
        this.window = window;
        this.toasts = toasts ?? throw new ArgumentNullException(nameof(toasts));

        foreach (SkillEntry entry in skills)
        {
            if (entry?.data == null) continue;

            rows.Add(new SkillRowModel(entry.data.Id, entry.icon, entry.data.SkillName, $"Lv.{entry.data.RequiredLevel}"));
            skillsById[entry.data.Id] = entry;
        }

        view.SetSkills(rows);
        view.SetSlotCount(stats.EquippedSlots.Value.Count);

        stats.LearnedSkills.Subscribe(RenderLearned).AddTo(disposables);
        // 레드닷: 지금 배울 수 있는 줄. 판정은 진행도의 학습 조건(CanLearnSkill)이고, 레벨이나 배운 목록이 바뀌면 다시 그린다.
        stats.Level.Subscribe(_ => RenderLearnable()).AddTo(disposables);
        stats.LearnedSkills.Subscribe(_ => RenderLearnable()).AddTo(disposables);
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

    private void RenderLearnable()
    {
        foreach (SkillRowModel row in rows) view.SetSkillLearnable(row.Id, stats.CanLearnSkill(row.Id));
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

    // 학습 조건은 진행도가 판정한다. 레벨이 모자라면 몇 레벨부터인지 알린다.
    // 이미 배운 스킬의 더블탭은 선택/해제의 연속이라 알리지 않는다. 없는 스킬은 데이터 오류라 로그만 남긴다.
    private void HandleSkillDoubleTapped(int skillId)
    {
        switch (stats.LearnSkill(skillId))
        {
            case LearnSkillResult.LevelTooLow:
                if (skillsById.TryGetValue(skillId, out SkillEntry entry)) toasts.Show(FormatLevelTooLow(entry.data.RequiredLevel));
                break;
            case LearnSkillResult.UnknownSkill:
                UnityEngine.Debug.LogError($"[SkillManagePresenter] 게임 데이터에 없는 스킬입니다: {skillId}");
                break;
        }
    }

    // "레벨 2가/3이"처럼 숫자에 따라 조사가 바뀌지 않게 "부터"를 쓴다.
    public static string FormatLevelTooLow(int requiredLevel) => $"Lv.{requiredLevel}부터 배울 수 있습니다";

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
