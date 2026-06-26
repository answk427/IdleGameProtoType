using Newtonsoft.Json;
using UnityEngine;

// 플레이어 레벨 1개에 대한 기본 스탯 엔트리 (SO 전용, MonsterData/StageData와 동일 패턴).
[System.Serializable]
public class PlayerStatData : IData
{
    public int Key => level;

    [SerializeField] private int level;
    [SerializeField] private int requiredExp;
    [SerializeField] private int baseMaxHp;
    [SerializeField] private int baseAttackDamage;
    [SerializeField] private float baseAttackInterval;

    [JsonProperty("level")]
    public int Level { get => level; private set => level = value; }
    [JsonProperty("requiredExp")]
    public int RequiredExp { get => requiredExp; private set => requiredExp = value; }
    [JsonProperty("baseMaxHp")]
    public int BaseMaxHp { get => baseMaxHp; private set => baseMaxHp = value; }
    [JsonProperty("baseAttackDamage")]
    public int BaseAttackDamage { get => baseAttackDamage; private set => baseAttackDamage = value; }
    [JsonProperty("baseAttackInterval")]
    public float BaseAttackInterval { get => baseAttackInterval; private set => baseAttackInterval = value; }
}
