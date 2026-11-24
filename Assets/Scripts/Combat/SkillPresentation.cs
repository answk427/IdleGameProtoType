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

    public static void PlayHit(Skill skill, Vector3[] hitPositions)
    {
        SkillEntry entry = skill?.Entry;
        if (entry == null || hitPositions == null) return;

        for (int i = 0; i < hitPositions.Length; i++)
        {
            if (entry.hitVfxPrefab != null && CombatEffectManager.Instance != null)
                CombatEffectManager.Instance.PlayVfx(entry.hitVfxPrefab, hitPositions[i]);

            if (entry.hitSfx != null)
                AudioSource.PlayClipAtPoint(entry.hitSfx, hitPositions[i]);
        }
    }
}
