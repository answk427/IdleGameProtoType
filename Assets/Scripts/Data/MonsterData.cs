using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class MonsterData : IData
{
    public int Key => _id;

    [SerializeField] private int _id;
    [SerializeField] private string _monsterName;
    [SerializeField] private int _maxHp;
    [SerializeField] private int _goldReward;
    [SerializeField] private int _expReward;
    [SerializeField] private int _attackDamage;
    [SerializeField] private float _attackInterval;
    [SerializeField] private float _attackRange;
    
    [JsonProperty("id")]
    public int id { get => _id; private set => _id = value; }
    [JsonProperty("goldReward")]
    public int goldReward { get => _goldReward; private set => _goldReward = value; }
    [JsonProperty("expReward")]
    public int expReward { get => _expReward; private set => _expReward = value; }
    [JsonProperty("monsterName")]
    public string monsterName { get => _monsterName; private set => _monsterName = value; }
    [JsonProperty("maxHp")]
    public int maxHp { get => _maxHp; private set => _maxHp = value; }
    [JsonProperty("attackDamage")]
    public int attackDamage { get => _attackDamage; private set => _attackDamage = value; }
    [JsonProperty("attackInterval")]
    public float attackInterval { get => _attackInterval; private set => _attackInterval = value; }
    [JsonProperty("attackRange")]
    public float attackRange { get => _attackRange; private set => _attackRange = value; }
}

