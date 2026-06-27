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
        player.PlayAnimationTrigger(skill.Data.AnimationTrigger);

        SkillEntry entry = skill.Entry;
        if (entry == null) return;

        if (entry.castVfxPrefab != null)
            CombatEffectManager.Instance.PlayVfx(entry.castVfxPrefab, player.transform.position);

        if (entry.castSfx != null)
            AudioSource.PlayClipAtPoint(entry.castSfx, player.transform.position);
    }

    protected override void DoAction()
    {
        // 스킬 한번 사용 후 공격상태로 복귀
        if (isAnimationFinished)
        {
            player.fsm.ChangeState(new PlayerAttackState(player, target));
        }
    }

    protected override void OnHitImpact()
    {
        // 효과가 실제로 적용된 위치는 효과마다 다르다(데미지는 타겟, 힐은 캐스터 자신, AOE는 맞은 모든 타겟)
        Vector3[] hitPositions = skill.Use(player, target);

        SkillEntry entry = skill.Entry;
        if (entry == null) return;

        for (int i = 0; i < hitPositions.Length; i++)
        {
            if (entry.hitVfxPrefab != null)
                CombatEffectManager.Instance.PlayVfx(entry.hitVfxPrefab, hitPositions[i]);

            if (entry.hitSfx != null)
                AudioSource.PlayClipAtPoint(entry.hitSfx, hitPositions[i]);
        }
    }
}
