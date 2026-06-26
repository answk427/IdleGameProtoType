using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "PlayerStatDatabase", menuName = "Data/Player Stat Database")]
public class PlayerStatDatabase : ScriptableObject
{
    public List<PlayerStatData> entries = new List<PlayerStatData>();

    private Dictionary<int, PlayerStatData> lookup;

    public int MaxLevel => entries.Count;

    // 레벨에 해당하는 스탯 반환. 못 찾으면 가장 가까운 레벨로 clamp.
    public PlayerStatData GetByLevel(int level)
    {
        EnsureLookup();

        if (lookup.TryGetValue(level, out var stat)) return stat;
        if (entries.Count == 0) return null;

        int clampedIndex = Mathf.Clamp(level - 1, 0, entries.Count - 1);
        return entries[clampedIndex];
    }

    // 레벨업에 필요한 경험치 반환
    public int GetRequiredExp(int level)
    {
        return GetByLevel(level)?.RequiredExp ?? 0;
    }

    public void EnsureLookup()
    {
        if (lookup != null) return;

        lookup = new Dictionary<int, PlayerStatData>();
        foreach (var e in entries)
        {
            if (e == null) continue;
            lookup[e.Level] = e;
        }
    }

    private void OnValidate()
    {
        lookup = null; // 인스펙터에서 직접 수정하면 캐시 무효화
    }
}
