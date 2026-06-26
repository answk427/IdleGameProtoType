using UnityEngine;

public class PlayerAttackState : PlayerCombatState
{
    private float timer;

    public PlayerAttackState(PlayerController playerController, IDamageable target) : base(playerController, target)
    {
    }

    public override void Enter()
    {
        base.Enter();

        timer = player.AttackInterval;
        isAnimationFinished = true;

        if (GameManager.Instance != null) GameManager.Instance.IsScrolling = false;
    }

    protected override void DoAction()
    {
        timer += Time.deltaTime;

        // 현재 애니메이션(공격/스킬)이 끝나기 전에는 새 행동을 시작하지 않는다.
        if (!isAnimationFinished) return;

        Skill readySkill = player.GetReadySkill();
        if (readySkill != null)
        {
            player.fsm.ChangeState(new PlayerSkillState(player, target, readySkill));
            return;
        }

        if (timer >= player.AttackInterval)
        {
            player.Attack();
            isAnimationFinished = false; // 새 공격 시작, 끝날 때까지 다음 행동 보류
            timer = 0f;
        }
    }

    protected override void OnHitImpact()
    {
        target?.TakeDamage(player.GetCalculatedDamage());

        if (target != null)
            CombatEffectManager.Instance.SpawnHitParticle(target.Position);
    }
}
