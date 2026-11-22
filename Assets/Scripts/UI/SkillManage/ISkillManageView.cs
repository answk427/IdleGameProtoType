using System;
using System.Collections.Generic;
using UnityEngine;

// Presenter가 아는 스킬 관리 창의 전부. uGUI 타입이 없어서, 프레임워크를 바꿔도 View만 다시 만들면 된다.
public interface ISkillManageView
{
    event Action<int> SkillTapped;       // 스킬 id
    event Action<int> SkillDoubleTapped; // 스킬 id
    event Action<int> SlotTapped;        // 슬롯 번호
    event Action CloseTapped;

    void SetSkills(IReadOnlyList<SkillRowModel> rows);
    void SetSkillLearned(int skillId, bool learned);
    void SetSelectedSkill(int skillId);

    void SetSlotCount(int count);
    void ShowSlotSkill(int slot, Sprite icon);
    void ClearSlot(int slot);
    void SetSlotCooldown(int slot, float ratio);
}

// 목록 한 줄의 고정 표시값. 문자열은 Presenter가 만들어서 넘긴다.
public readonly struct SkillRowModel
{
    public int Id { get; }
    public Sprite Icon { get; }
    public string Name { get; }
    public string Requirement { get; }

    public SkillRowModel(int id, Sprite icon, string name, string requirement)
    {
        Id = id;
        Icon = icon;
        Name = name;
        Requirement = requirement;
    }
}
