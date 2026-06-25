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

        // 들어오자마자 바로 공격할 수 있게
        timer = player.AttackInterval;

        if (GameManager.Instance != null) GameManager.Instance.IsScrolling = false;
    }

    protected override void DoAction()
    {
        Skill readySkill = player.GetReadySkill();
        if (readySkill != null)
        {
            player.fsm.ChangeState(new PlayerSkillState(player, target, readySkill));
            return;
        }

        timer += Time.deltaTime;
        if (timer >= player.AttackInterval)
        {
            player.Attack();
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
