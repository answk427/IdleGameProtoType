using UnityEngine;
using Newtonsoft.Json;

[System.Serializable]
public class MonsterData : IData
{
    public int Key => id;

    [SerializeField] private int id;
    [SerializeField] private string monsterName;
    [SerializeField] private int maxHp;
    [SerializeField] private int goldReward;
    [SerializeField] private int expReward;
    [SerializeField] private int attackDamage;
    [SerializeField] private float attackInterval;
    [SerializeField] private float attackRange;
    [SerializeField] private int skillId;

    [JsonProperty("id")]
    public int Id { get => id; private set => id = value; }
    [JsonProperty("goldReward")]
    public int GoldReward { get => goldReward; private set => goldReward = value; }
    [JsonProperty("expReward")]
    public int ExpReward { get => expReward; private set => expReward = value; }
    [JsonProperty("monsterName")]
    public string MonsterName { get => monsterName; private set => monsterName = value; }
    [JsonProperty("maxHp")]
    public int MaxHp { get => maxHp; private set => maxHp = value; }
    [JsonProperty("attackDamage")]
    public int AttackDamage { get => attackDamage; private set => attackDamage = value; }
    [JsonProperty("attackInterval")]
    public float AttackInterval { get => attackInterval; private set => attackInterval = value; }
    [JsonProperty("attackRange")]
    public float AttackRange { get => attackRange; private set => attackRange = value; }
    [JsonProperty("skillId")]
    public int SkillId { get => skillId; private set => skillId = value; }
}

