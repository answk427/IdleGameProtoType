using System.Collections.Generic;
using System.IO;
using UnityEngine;

// 플레이어 저장 데이터 (JSON 직렬화 대상)
// 레벨/경험치/업그레이드 횟수만 저장 — 최종 스탯은 런타임에 계산
[System.Serializable]
public class PlayerSaveData
{
    public const int EmptySlot = -1;
    public const int SkillSlotCount = 5;

    public int level = 1;
    public int currentExp = 0;

    // 업그레이드 횟수 (돈으로 구매한 누적 횟수)
    public int hpUpgradeLevel = 0;
    public int attackUpgradeLevel = 0;
    public int speedUpgradeLevel = 0;

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

    // ── 저장/불러오기 ──────────────────────────────────────

    private static string SavePath => Path.Combine(Application.persistentDataPath, "player_save.json");

    public void Save()
    {
        string json = JsonUtility.ToJson(this, prettyPrint: true);
        File.WriteAllText(SavePath, json);
        Debug.Log($"[PlayerSaveData] 저장 완료: {SavePath}");
    }

    public static PlayerSaveData Load()
    {
        if (!File.Exists(SavePath))
        {
            Debug.Log("[PlayerSaveData] 저장 파일 없음 → 신규 데이터 생성");
            return new PlayerSaveData();
        }

        string json = File.ReadAllText(SavePath);
        var data = JsonUtility.FromJson<PlayerSaveData>(json);
        Debug.Log($"[PlayerSaveData] 불러오기 완료 (레벨 {data.level})");
        return data;
    }

    public static void DeleteSave()
    {
        if (File.Exists(SavePath))
            File.Delete(SavePath);
        Debug.Log("[PlayerSaveData] 저장 파일 삭제");
    }
}
