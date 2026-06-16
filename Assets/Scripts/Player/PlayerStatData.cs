using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 레벨별 기본 스탯 테이블 (ScriptableObject)
/// 디자이너가 인스펙터에서 수치 조정
/// </summary>
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

#if UNITY_EDITOR
    /// <summary>에디터에서 레벨 테이블 자동 생성 (개발 편의용)</summary>
    [ContextMenu("기본 테이블 자동 생성 (30레벨)")]
    private void GenerateDefaultTable()
    {
        levelStats.Clear();
        for (int i = 1; i <= 30; i++)
        {
            levelStats.Add(new LevelStat
            {
                level = i,
                requiredExp = 50 + (i - 1) * 30,          // 50, 80, 110 ...
                baseMaxHp = 100 + (i - 1) * 20,            // 100, 120, 140 ...
                baseAttackDamage = 5 + (i - 1) * 2,        // 5, 7, 9 ...
                baseAttackInterval = Mathf.Max(0.4f, 1f - (i - 1) * 0.02f), // 1.0 → 0.4 까지 감소
                baseRunSpeed = 2f + (i - 1) * 0.05f        // 2.0, 2.05, 2.1 ...
            });
        }
        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[PlayerStatData] 기본 테이블 30레벨 생성 완료");
    }
#endif
}
