using System.Collections.Generic;

[System.Serializable]
public class PlayerStatsSaveData
{
    public const int EmptySlot = -1;
    public const int SkillSlotCount = 5;

    public int level = 1;
    public int currentExp = 0;

    // 업그레이드 횟수 (돈으로 구매한 누적 횟수)
    public int hpUpgradeLevel = 0;
    public int attackUpgradeLevel = 0;
    public int attackSpeedUpgradeLevel = 0;

    // 학습한 스킬 id 목록
    public List<int> learnedSkillIds = new List<int>();

    // 전투 슬롯에 배치된 스킬 id (빈 슬롯은 EmptySlot)
    public int[] equippedSkillSlotIds = NewEmptySlots();

    private static int[] NewEmptySlots()
    {
        var slots = new int[SkillSlotCount];
        for (int i = 0; i < slots.Length; i++) slots[i] = EmptySlot;
        return slots;
    }
}
