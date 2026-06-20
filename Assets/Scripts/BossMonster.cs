using System;
using UnityEngine;

public class BossMonster : Monster
{
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackInterval = 2.0f;

    public event Action OnHitEvent;
    public event Action OnAttackEndEvent;

    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval;
    public int AttackDamage => attackDamage;

    protected override void OnEnable()
    {
        Debug.Log("BossMonster OnEnable");
        IsAlive = true;
        fsm.ChangeState(new BossRunState(this));
    }

    protected override void Die()
    {
        base.Die();
        GlobalGameEvents.TriggerBossKilled();
    }

    public void Attack()
    {
        Debug.Log("Boss Attack");
        PlayAttackAnimation();
    }

    public override void Initialize(MonsterData monsterData, float goldMultiplier, float hpMultiplier, float dmgMultiplier)
    {
        base.Initialize(monsterData, goldMultiplier, hpMultiplier, dmgMultiplier);
        this.attackRange = monsterData.attackRange;
        this.attackInterval = monsterData.attackInterval;
    }

    public int GetCalculatedDamage()
    {
        return attackDamage;
    }

    // 애니메이션 이벤트가 호출할 함수 (Hit 타이밍)
    public void AE_OnHit()
    {
        OnHitEvent?.Invoke();
    }

    // 애니메이션 이벤트가 호출할 함수 (애니메이션 끝난 시점)
    public void AE_OnAttackEnd()
    {
        OnAttackEndEvent?.Invoke();
    }

    public void PlayAttackAnimation()
    {
        PlayTrigger("Attack");
    }
}
