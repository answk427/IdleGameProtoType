using UnityEngine;

// 스킬 시전/타격 연출(이펙트, 소리). CombatEffectManager가 전투 사건(ActionStarted, SkillHit)을 받아 부른다(5-A).
// 스킬의 연출 자산(VFX 프리팹, 소리)은 SkillEntry에 있고, 사건은 스킬 id만 싣는다. 전투 규칙은 이것을 모른다(P7).
public static class SkillPresentation
{
    public static void PlayCast(SkillEntry entry, Vector3 casterPosition)
    {
        if (entry == null) return;

        if (entry.castVfxPrefab != null && CombatEffectManager.Instance != null)
            CombatEffectManager.Instance.PlayVfx(entry.castVfxPrefab, casterPosition);

        if (entry.castSfx != null)
            AudioSource.PlayClipAtPoint(entry.castSfx, casterPosition);
    }

    // 맞은 전투원 하나의 몸 위치에 재생한다(맞은 수만큼 사건이 온다).
    public static void PlayHit(SkillEntry entry, Vector3 targetPosition)
    {
        if (entry == null) return;

        if (entry.hitVfxPrefab != null && CombatEffectManager.Instance != null)
            CombatEffectManager.Instance.PlayVfx(entry.hitVfxPrefab, targetPosition);

        if (entry.hitSfx != null)
            AudioSource.PlayClipAtPoint(entry.hitSfx, targetPosition);
    }
}
