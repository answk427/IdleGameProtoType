using UnityEngine;

public abstract class BossCombatState : IState
{
    protected BossMonster boss;
    protected IDamageable target;

    protected bool isAnimationFinished = false;

    public BossCombatState(BossMonster boss, IDamageable target)
    {
        this.boss = boss;
        this.target = target;
    }

    public virtual void Enter()
    {
        boss.OnHitEvent += HandleHitInternal;
        boss.OnAttackEndEvent += HandleAttackEndInternal;

        isAnimationFinished = false;
    }

    public void Execute()
    {
        if (target != null && target.IsAlive)
        {
            DoAction();
        }
    }

    public virtual void Exit()
    {
        boss.OnHitEvent -= HandleHitInternal;
        boss.OnAttackEndEvent -= HandleAttackEndInternal;
    }

    private void HandleHitInternal()
    {
        if (target != null && target.IsAlive)
        {
            OnHitImpact();
        }
    }

    private void HandleAttackEndInternal()
    {
        isAnimationFinished = true;
    }

    protected abstract void DoAction();    // 쿨타임 및 공격 지시 로직
    protected abstract void OnHitImpact(); // 실제 데미지 처리
}
