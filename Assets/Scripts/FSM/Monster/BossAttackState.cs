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
        isAnimationFinished = true;
    }

    protected override void DoAction()
    {
        timer += Time.deltaTime;

        // 현재 애니메이션(공격/스킬)이 끝나기 전에는 새 행동을 시작하지 않는다.
        if (!isAnimationFinished) return;

        if (boss.Skill != null && boss.Skill.IsReady)
        {
            boss.fsm.ChangeState(new BossSkillState(boss, target, boss.Skill));
            return;
        }

        if (timer >= boss.AttackInterval)
        {
            boss.Attack();
            isAnimationFinished = false; // 새 공격 시작, 끝날 때까지 다음 행동 보류
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
