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
        // 스킬은 1회성 캐스팅 — 쿨타임 루프 없음, 애니메이션 종료는 base.Execute()가 처리
    }

    protected override void OnHitImpact()
    {
        skill.Use(player, target);

        SkillEntry entry = skill.Entry;
        if (entry == null) return;

        Vector3 hitPos = target is Monster m ? m.transform.position : player.transform.position;

        if (entry.hitVfxPrefab != null)
            CombatEffectManager.Instance.PlayVfx(entry.hitVfxPrefab, hitPos);

        if (entry.hitSfx != null)
            AudioSource.PlayClipAtPoint(entry.hitSfx, hitPos);
    }
}
