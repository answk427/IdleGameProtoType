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

        // 들어오자마자 바로 행동할 수 있게(스킬이 준비돼 있으면 기본공격을 강제로
        // 한 번 거치지 않고 바로 스킬을 쓸 수 있어야 한다). base.Enter()가 false로
        // 리셋해두므로 여기서 다시 true로 덮어쓴다 — "진행 중인 애니메이션이 없다"는 뜻.
        timer = player.AttackInterval;
        isAnimationFinished = true;

        if (GameManager.Instance != null) GameManager.Instance.IsScrolling = false;
    }

    protected override void DoAction()
    {
        timer += Time.deltaTime;

        // 현재 애니메이션(공격/스킬)이 끝나기 전에는 새 행동을 시작하지 않는다.
        // 이걸 안 지키면 진행 중인 공격의 타격(AE_OnHit)이 적용되기 전에 스킬로
        // 끼어들어서 데미지가 통째로 스킵되는 문제가 있었다.
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
