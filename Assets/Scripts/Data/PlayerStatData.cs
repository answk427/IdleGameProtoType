using Newtonsoft.Json;
using UnityEngine;

// 플레이어 레벨 1개에 대한 기본 스탯 엔트리 (SO 전용, MonsterData/StageData와 동일 패턴).
// Excel → ExcelToJsonConverter → PlayerStatDatabaseSyncer 경로로 채워진다.
[System.Serializable]
public class PlayerStatData : IData
{
    public int Key => _level;

    [SerializeField] private int _level;
    [SerializeField] private int _requiredExp;
    [SerializeField] private int _baseMaxHp;
    [SerializeField] private int _baseAttackDamage;
    [SerializeField] private float _baseAttackInterval;

    [JsonProperty("level")]
    public int level { get => _level; private set => _level = value; }
    [JsonProperty("requiredExp")]
    public int requiredExp { get => _requiredExp; private set => _requiredExp = value; }
    [JsonProperty("baseMaxHp")]
    public int baseMaxHp { get => _baseMaxHp; private set => _baseMaxHp = value; }
    [JsonProperty("baseAttackDamage")]
    public int baseAttackDamage { get => _baseAttackDamage; private set => _baseAttackDamage = value; }
    [JsonProperty("baseAttackInterval")]
    public float baseAttackInterval { get => _baseAttackInterval; private set => _baseAttackInterval = value; }
}
