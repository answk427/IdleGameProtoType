using System.Collections.Generic;
using UnityEngine;


[CreateAssetMenu(fileName = "PlayerStatData", menuName = "IdleGame/Player Stat Data")]
public class PlayerStatData : ScriptableObject
{
    [System.Serializable]
    public class LevelStat
    {
        [Header("레벨")]
        public int level;

        [Header("필요 경험치 (이 레벨 → 다음 레벨)")]
        public int requiredExp;

        [Header("기본 스탯")]
        public int baseMaxHp;
        public int baseAttackDamage;
        public float baseAttackInterval;
        public float baseRunSpeed;
    }

    [SerializeField] private List<LevelStat> levelStats = new();

    public int MaxLevel => levelStats.Count;

    /// <summary>레벨에 해당하는 스탯 반환 (1-based)</summary>
    public LevelStat GetStat(int level)
    {
        int index = Mathf.Clamp(level - 1, 0, levelStats.Count - 1);
        return levelStats[index];
    }

    /// <summary>레벨업에 필요한 경험치 반환</summary>
    public int GetRequiredExp(int level)
    {
        return GetStat(level).requiredExp;
    }
}
