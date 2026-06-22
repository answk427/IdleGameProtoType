using UnityEngine;

public class PlayerSkillState : PlayerCombatState
{
    private readonly Skill skill;

    public PlayerSkillState(PlayerController player, IDamageable target, Skill skill)
        : base(player, target)
    {
        this.skill = skill;
    }

    public override void Enter()
    {
        base.Enter();
        player.PlayAnimationTrigger(skill.Data.animationTrigger);

        SkillEntry entry = skill.Entry;
        if (entry == null) return;

        if (entry.castVfxPrefab != null)
            CombatEffectManager.Instance.PlayVfx(entry.castVfxPrefab, player.transform.position);

        if (entry.castSfx != null)
            AudioSource.PlayClipAtPoint(entry.castSfx, player.transform.position);
    }

    protected override void DoAction()
    {
        // 스킬은 1회성 캐스팅 — 쿨타임 루프 없음.
        // 타겟이 죽었다면 base.Execute()가 애니메이션 종료 후 Run으로 전환해주지만,
        // 타겟이 살아있는 채로 애니메이션만 끝난 경우는 여기서 직접 공격 상태로 복귀시켜야 한다.
        // (그냥 두면 DoAction이 매 프레임 호출되는데도 아무 전환이 없어 PlayerSkillState에 영원히 멈춰버림)
        // DoAction()은 base.Execute()에서 target.IsAlive가 true일 때만 호출되므로 별도 생존 체크는 불필요.
        if (isAnimationFinished)
        {
            player.fsm.ChangeState(new PlayerAttackState(player, target));
        }
    }

    protected override void OnHitImpact()
    {
        // 효과가 실제로 적용된 위치는 효과마다 다르다(데미지는 타겟, 힐은 캐스터 자신 등) —
        // 여기서 추측하지 않고 Skill.Use()의 반환값을 그대로 사용한다.
        Vector3 hitPos = skill.Use(player, target);

        SkillEntry entry = skill.Entry;
        if (entry == null) return;

        if (entry.hitVfxPrefab != null)
            CombatEffectManager.Instance.PlayVfx(entry.hitVfxPrefab, hitPos);

        if (entry.hitSfx != null)
            AudioSource.PlayClipAtPoint(entry.hitSfx, hitPos);
    }
}
