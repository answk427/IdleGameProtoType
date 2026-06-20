using UnityEngine;

public abstract class PlayerCombatState : IState
{
    protected PlayerController player;
    protected IDamageable target;

    //애니메이션이 완전히 끝났는지 확인
    private bool isAnimationFinished = false;

    public PlayerCombatState(PlayerController player, IDamageable target)
    {
        this.player = player;
        this.target = target;
    }

    public virtual void Enter()
    {
        Debug.Log("PlayerCombatState Enter()");
        player.OnHitEvent += HandleHitInternal;
        player.OnAttackEndEvent += HandleAttackEndInternal;

        isAnimationFinished = false;
    }

    public void Execute()
    {
        Debug.Log($"PlayerCombatState Execute, targetLive : {target.IsAlive}");
        // 대상이 사라졌고 공격 애니메이션까지 끝났다면 달림
        if ((target == null || !target.IsAlive ) && isAnimationFinished)
        {
            player.fsm.ChangeState(new PlayerRunState(player));
            return;
        }

        // 대상이 아직 살아있을 때만 자식들의 고유 행동(쿨타임 계산 등)을 실행
        if (target != null && target.IsAlive)
        {
            DoAction();
        }
    }

    public void Exit()
    {
        player.OnHitEvent -= HandleHitInternal;
        player.OnAttackEndEvent -= HandleAttackEndInternal;
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

    protected abstract void DoAction(); // 쿨타임 및 공격 지시 로직
    protected abstract void OnHitImpact(); // 진짜 데미지를 주는 로직
}
