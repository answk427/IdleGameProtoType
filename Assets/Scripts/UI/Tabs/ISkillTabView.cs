using System;
using UnityEngine;

// Presenter가 아는 스킬 탭(장착 슬롯 바)의 전부.
public interface ISkillTabView
{
    event Action<int> SlotTapped; // 슬롯 번호
    event Action ManageTapped;    // 스킬 관리 창 열기

    void SetSlotCount(int count);
    void ShowSlotSkill(int slot, Sprite icon);
    void ClearSlot(int slot);
    void SetSlotCooldown(int slot, float ratio);
}
