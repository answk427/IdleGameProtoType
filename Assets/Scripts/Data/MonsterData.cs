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
    // 교전하지 않을 때 움직이는 방식(Hold: 제자리, Approach: 다가옴). 기본값 Hold(docs/COMBAT_ARCHITECTURE.md 8-4).
    [SerializeField] private MovePolicy movePolicy;
    // 이동 속도(월드 유닛/초). 접근(Approach) 정책이 쓴다(docs/COMBAT_ARCHITECTURE.md 9-3, D17).
    [SerializeField] private float moveSpeed;

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
    [JsonProperty("movePolicy")]
    public MovePolicy MovePolicy { get => movePolicy; private set => movePolicy = value; }
    [JsonProperty("moveSpeed")]
    public float MoveSpeed { get => moveSpeed; private set => moveSpeed = value; }
}

