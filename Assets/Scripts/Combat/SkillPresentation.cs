using System.Collections.Generic;
using UnityEngine;

// 스킬 시전/타격 연출(이펙트, 소리). 플레이어와 보스가 같이 쓴다.
// 전투 로직이 연출을 직접 부르는 구조(P7)는 5단계에서 이벤트로 바꾼다. 그때까지 부르는 곳을 여기 한 곳으로 모아 둔다.
public static class SkillPresentation
{
    public static void PlayCast(Skill skill, Vector3 casterPosition)
    {
        SkillEntry entry = skill?.Entry;
        if (entry == null) return;

        if (entry.castVfxPrefab != null && CombatEffectManager.Instance != null)
            CombatEffectManager.Instance.PlayVfx(entry.castVfxPrefab, casterPosition);

        if (entry.castSfx != null)
            AudioSource.PlayClipAtPoint(entry.castSfx, casterPosition);
    }

    // 맞은 전투원마다 그 몸 위치에 재생한다.
    public static void PlayHit(Skill skill, IReadOnlyList<Combatant> hitTargets)
    {
        SkillEntry entry = skill?.Entry;
        if (entry == null || hitTargets == null) return;

        for (int i = 0; i < hitTargets.Count; i++)
        {
            Vector3 position = CombatantHost.EffectPositionOf(hitTargets[i]);

            if (entry.hitVfxPrefab != null && CombatEffectManager.Instance != null)
                CombatEffectManager.Instance.PlayVfx(entry.hitVfxPrefab, position);

            if (entry.hitSfx != null)
                AudioSource.PlayClipAtPoint(entry.hitSfx, position);
        }
    }
}
