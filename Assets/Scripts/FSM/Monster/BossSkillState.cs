using UnityEngine;

public class BossSkillState : BossCombatState
{
    private readonly Skill skill;

    public BossSkillState(BossMonster boss, IDamageable target, Skill skill) : base(boss, target)
    {
        this.skill = skill;
    }

    public override void Enter()
    {
        base.Enter();
        boss.PlayAnimationTrigger(skill.Data.AnimationTrigger);

        SkillEntry entry = skill.Entry;
        if (entry == null) return;

        if (entry.castVfxPrefab != null)
            CombatEffectManager.Instance.PlayVfx(entry.castVfxPrefab, boss.transform.position);

        if (entry.castSfx != null)
            AudioSource.PlayClipAtPoint(entry.castSfx, boss.transform.position);
    }

    protected override void DoAction()
    {
        // 스킬 한 번 사용 후 공격 상태로 복귀
        if (isAnimationFinished)
        {
            boss.fsm.ChangeState(new BossAttackState(boss, target));
        }
    }

    protected override void OnHitImpact()
    {
        // 효과가 실제로 적용된 위치는 효과마다 다르다(데미지는 타겟, 힐은 캐스터 자신, AOE는 맞은 모든 타겟)
        Vector3[] hitPositions = skill.Use(boss, target);

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
