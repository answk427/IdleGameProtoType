using System;
using System.Collections.Generic;
using UnityEngine;

public class BossMonster : Monster, ISkillCaster
{
    [SerializeField] private float attackRange = 1.5f;
    [SerializeField] private float attackInterval = 2.0f;

    private int attackDamage;
    private Skill skill;

    public event Action OnHitEvent;
    public event Action OnAttackEndEvent;

    public float AttackRange => attackRange;
    public float AttackInterval => attackInterval;
    public int AttackDamage => attackDamage;
    public Skill Skill => skill;

    protected override void OnEnable()
    {
        IsAlive = true;
        HalfWidth = halfWidthOverride > 0f ? halfWidthOverride : CombatRangeUtility.GetHalfWidth(gameObject);
        fsm.ChangeState(new BossRunState(this));
    }

    protected override void Update()
    {
        base.Update();
        skill?.Tick(Time.deltaTime);
    }

    protected override void Die()
    {
        base.Die();
        GlobalGameEvents.TriggerBossKilled();
    }

    public void Attack()
    {
        PlayAttackAnimation();
    }

    public override void Initialize(MonsterData monsterData, float goldMultiplier, float hpMultiplier, float dmgMultiplier)
    {
        base.Initialize(monsterData, goldMultiplier, hpMultiplier, dmgMultiplier);
        this.attackRange = monsterData.AttackRange;
        this.attackInterval = monsterData.AttackInterval;
        this.attackDamage = Mathf.RoundToInt(monsterData.AttackDamage * dmgMultiplier);

        SkillEntry entry = monsterData.SkillId > 0 ? GameDatabaseManager.Instance?.GetSkill(monsterData.SkillId) : null;
        this.skill = entry?.data != null ? new Skill(entry) : null;
    }

    public int GetCalculatedDamage()
    {
        return attackDamage;
    }

    public int GetCalculatedDamage(float skillMultiplier)
    {
        return Mathf.RoundToInt(attackDamage * skillMultiplier);
    }

    public void Heal(int amount) => HealInternal(amount);

    // 보스의 적은 항상 플레이어 1명뿐이라 단순 거리 체크로 충분.
    public List<IDamageable> GetOpponentsInRange(float radius)
    {
        List<IDamageable> result = new List<IDamageable>();
        PlayerController player = GameManager.Instance != null ? GameManager.Instance.GetPlayer() : null;

        if (player != null && Mathf.Abs(player.transform.position.x - transform.position.x) <= radius)
        {
            result.Add(player);
        }

        return result;
    }

    public void PlayAnimationTrigger(string triggerName)
    {
        PlayTrigger(triggerName);
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
