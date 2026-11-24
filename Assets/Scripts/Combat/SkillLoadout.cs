using System;
using System.Collections.Generic;
using UnityEngine;

// 플레이어의 스킬 런타임 상태(docs/COMBAT_ARCHITECTURE.md 7-6). 순수 C#.
//
// 쿨다운은 슬롯이 아니라 스킬 id에 붙는다. 처음 장착할 때 인스턴스를 만들고, 장착을 풀어도 버리지 않는다.
// 예전에는 슬롯마다 인스턴스를 새로 만들어서, 스킬을 다른 슬롯으로 옮기기만 해도 쿨다운이 0이 됐다.
// 쿨다운은 준비 시각(절대 시각)이라 장착하지 않은 동안에도 흐른다(결정 S2). 저장하지 않는다(앱을 다시 켜면 0).
public sealed class SkillLoadout
{
    public const int EmptySlot = -1;

    private readonly Func<int, SkillEntry> resolveEntry;
    private readonly Dictionary<int, Skill> skillsById = new Dictionary<int, Skill>();
    private readonly Skill[] slots;

    public SkillLoadout(int slotCount, Func<int, SkillEntry> resolveEntry)
    {
        if (slotCount < 0) throw new ArgumentOutOfRangeException(nameof(slotCount));
        slots = new Skill[slotCount];
        this.resolveEntry = resolveEntry ?? throw new ArgumentNullException(nameof(resolveEntry));
    }

    public int SlotCount => slots.Length;

    public Skill GetSlot(int slotIndex) =>
        slotIndex >= 0 && slotIndex < slots.Length ? slots[slotIndex] : null;

    // 슬롯 배치를 스킬 id 목록으로 다시 맞춘다. 비어 있거나 모자란 칸은 빈 슬롯.
    public void SetSlots(IReadOnlyList<int> skillIds)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            int id = skillIds != null && i < skillIds.Count ? skillIds[i] : EmptySlot;
            slots[i] = id == EmptySlot ? null : GetOrCreate(id);
        }
    }

    // 슬롯 순서가 우선순위다.
    public Skill PickReady(double now)
    {
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].IsReadyAt(now)) return slots[i];
        }
        return null;
    }

    // now 이후 처음으로 장착 스킬이 준비되는 시각. 없으면 무한대.
    public double NextReadyTime(double now)
    {
        double next = double.PositiveInfinity;
        for (int i = 0; i < slots.Length; i++)
        {
            if (slots[i] != null && slots[i].ReadyAt > now && slots[i].ReadyAt < next) next = slots[i].ReadyAt;
        }
        return next;
    }

    private Skill GetOrCreate(int skillId)
    {
        if (skillsById.TryGetValue(skillId, out Skill skill)) return skill;

        SkillEntry entry = resolveEntry(skillId);
        if (entry?.data == null)
        {
            Debug.LogError($"[SkillLoadout] 스킬 {skillId}의 데이터가 없습니다. 슬롯은 비워 둡니다.");
            return null;
        }

        skill = new Skill(entry);
        skillsById.Add(skillId, skill);
        return skill;
    }
}
