using UnityEngine;

[System.Serializable]
public class MonsterData : IData
{
    public int id;
    public int ID => id;

    public string monsterName;
    public string prefabName;
    
    public int maxHp;
    public int goldReward;

    public int attackDamage;
    public float attackInterval;
    public float attackRange;
}
