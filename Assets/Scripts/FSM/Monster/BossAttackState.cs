using UnityEngine;

public class BossAttackState : BossCombatState
{
    private float timer;

    public BossAttackState(BossMonster boss, IDamageable target) : base(boss, target)
    {
    }

    public override void Enter()
    {
        base.Enter();

        // 들어오자마자 바로 공격할 수 있게
        timer = boss.AttackInterval;
    }

    protected override void DoAction()
    {
        timer += Time.deltaTime;
        if (timer >= boss.AttackInterval)
        {
            boss.Attack();
            timer = 0f;
        }
    }

    protected override void OnHitImpact()
    {
        target?.TakeDamage(boss.GetCalculatedDamage());

        if (target != null)
            CombatEffectManager.Instance.SpawnHitParticle(target.Position);
    }
}
