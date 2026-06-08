using UnityEngine;

[System.Serializable]
public class PlayerStats
{
    [SerializeField] private float runSpeed = 2f;
    [SerializeField] private int attackDamage = 5;
    [SerializeField] private float attackInterval = 1f;

    public float RunSpeed => runSpeed;
    public int AttackDamage => attackDamage;
    public float AttackInterval => Mathf.Max(attackInterval, 0.05f);

    public void AddAttackDamage(int amount)
    {
        attackDamage = Mathf.Max(0, attackDamage + amount);
    }

    public void MultiplyAttackInterval(float multiplier)
    {
        attackInterval = Mathf.Max(0.05f, attackInterval * multiplier);
    }
}