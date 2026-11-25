using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// 전투 데이터와 프리팹의 짝 검사(docs/COMBAT_ARCHITECTURE.md 8-7). ActionTimelineTools.ValidateCombatData가 부른다:
// 에디터가 열릴 때 에러 로그, 빌드 직전에는 빌드 실패.
// 런타임에 "시간표 없음" 에러로만 드러나던 어긋남(공격을 켰는데 공격 동작이 없음 등)을, 데이터를 고친 사람이 플레이 전에 알게 한다.
public static class CombatDataValidator
{
    public static List<string> ValidateDatabases()
    {
        var errors = new List<string>();
        ValidateCombatantPrefabs(errors);

        SkillDatabase skills = LoadFirst<SkillDatabase>();
        MonsterDatabase monsters = LoadFirst<MonsterDatabase>();
        ValidateMonsters(monsters, skills, errors);
        ValidateCharacters(LoadFirst<CharacterDatabase>(), skills, errors);
        ValidateStages(LoadFirst<StageDatabase>(), monsters, errors);
        ValidateSkillEffects(skills, errors);
        ValidateFlowTimings(LoadFirst<BalanceConfig>(), errors);
        return errors;
    }

    // 흐름의 대기 시간(D24): 0 이상의 유한한 값. 0이면 그 대기가 없다.
    private static void ValidateFlowTimings(BalanceConfig balance, List<string> errors)
    {
        if (balance == null)
        {
            errors.Add("[CombatData] BalanceConfig가 없습니다(흐름 시간과 성장 곡선).");
            return;
        }

        RequireWait(balance.encounterClearDelay, "encounterClearDelay", errors);
        RequireWait(balance.bossIntroDelay, "bossIntroDelay", errors);
        RequireWait(balance.transitionDuration, "transitionDuration", errors);
    }

    private static void RequireWait(double seconds, string name, List<string> errors)
    {
        if (!(seconds >= 0.0) || double.IsInfinity(seconds))
            errors.Add($"[CombatData] BalanceConfig: {name}는 0 이상이어야 합니다(0 = 대기 없음): {seconds}");
    }

    // 모든 전투원 프리팹: 판정 몸(반너비 > 0), 시간표, 애니메이션 뷰
    private static void ValidateCombatantPrefabs(List<string> errors)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            CombatantHost host = prefab != null ? prefab.GetComponent<CombatantHost>() : null;
            if (host == null) continue;

            var so = new SerializedObject(host);
            float halfWidth = so.FindProperty("hitboxProfile.halfWidth").floatValue;
            if (!(halfWidth > 0f) || float.IsInfinity(halfWidth))
                errors.Add($"[CombatData] {path}: 판정 몸의 반너비(hitboxProfile.halfWidth)는 0보다 커야 합니다: {halfWidth}");
            if (so.FindProperty("timelines").objectReferenceValue == null)
                errors.Add($"[CombatData] {path}: 행동 시간표(ActionTimelineSet)가 연결되지 않았습니다.");
            if (so.FindProperty("view").objectReferenceValue == null)
                errors.Add($"[CombatData] {path}: CharacterAnimationView가 연결되지 않았습니다.");
        }
    }

    private static void ValidateMonsters(MonsterDatabase monsters, SkillDatabase skills, List<string> errors)
    {
        if (monsters == null) return;

        foreach (MonsterEntry entry in monsters.entries)
        {
            MonsterData data = entry?.data;
            if (data == null) continue;
            string where = $"[CombatData] 몬스터 {data.Id}({data.MonsterName})";

            if (data.MaxHp <= 0) errors.Add($"{where}: maxHp는 0보다 커야 합니다: {data.MaxHp}");
            if (data.AttackDamage < 0) errors.Add($"{where}: attackDamage는 0 이상이어야 합니다: {data.AttackDamage}");
            if (!(data.AttackInterval >= 0f) || float.IsInfinity(data.AttackInterval))
                errors.Add($"{where}: attackInterval은 0 이상이어야 합니다(0 = 기본 공격 없음): {data.AttackInterval}");
            if (!(data.AttackRange >= 0f)) errors.Add($"{where}: attackRange는 0 이상이어야 합니다: {data.AttackRange}");
            if (data.MovePolicy != MovePolicy.Hold && data.MovePolicy != MovePolicy.Approach)
                errors.Add($"{where}: movePolicy는 Hold 또는 Approach여야 합니다: {data.MovePolicy}");
            if (!(data.MoveSpeed >= 0f) || float.IsInfinity(data.MoveSpeed))
                errors.Add($"{where}: moveSpeed는 0 이상이어야 합니다: {data.MoveSpeed}");
            else if (data.MovePolicy == MovePolicy.Approach && !(data.MoveSpeed > 0f))
                errors.Add($"{where}: 접근(Approach) 몬스터의 moveSpeed는 0보다 커야 합니다(0이면 다가오지 않는다)");

            Monster host = entry.prefab != null ? entry.prefab.GetComponent<Monster>() : null;
            if (host == null)
            {
                errors.Add($"{where}: 프리팹이 없거나 Monster 컴포넌트가 없습니다.");
                continue;
            }

            ActionTimelineSet timelines = host.Timelines;
            if (data.AttackInterval > 0f) RequireTimeline(timelines, ActionKeys.Attack, $"{where}: 공격 간격이 있는데(기본 공격) 프리팹 시간표에", errors);

            if (data.SkillId > 0)
            {
                SkillEntry skill = skills != null ? skills.GetById(data.SkillId) : null;
                if (skill?.data == null) errors.Add($"{where}: 스킬 {data.SkillId}이 SkillDatabase에 없습니다.");
                else RequireTimeline(timelines, skill.data.AnimationTrigger, $"{where}: 스킬 {skill.data.Id}({skill.data.SkillName})의 행동 키가 프리팹 시간표에", errors);
            }
        }
    }

    // 플레이어 캐릭터: 사거리·속도, 프리팹, 기본 공격·사망 동작, 그리고 배울 수 있는 모든 스킬의 동작
    private static void ValidateCharacters(CharacterDatabase characters, SkillDatabase skills, List<string> errors)
    {
        if (characters == null)
        {
            errors.Add("[CombatData] CharacterDatabase가 없습니다(엑셀 CharacterDatabase 시트를 변환하세요).");
            return;
        }

        foreach (CharacterEntry entry in characters.entries)
        {
            CharacterData data = entry?.data;
            if (data == null) continue;
            string where = $"[CombatData] 캐릭터 {data.Id}({data.CharacterName})";

            if (!(data.AttackRange >= 0f)) errors.Add($"{where}: attackRange는 0 이상이어야 합니다: {data.AttackRange}");
            if (!(data.MoveSpeed > 0f) || float.IsInfinity(data.MoveSpeed)) errors.Add($"{where}: moveSpeed는 0보다 커야 합니다: {data.MoveSpeed}");

            PlayerController host = entry.prefab != null ? entry.prefab.GetComponent<PlayerController>() : null;
            if (host == null)
            {
                errors.Add($"{where}: 프리팹이 없거나 PlayerController 컴포넌트가 없습니다.");
                continue;
            }

            ActionTimelineSet timelines = host.Timelines;
            RequireTimeline(timelines, ActionKeys.Attack, $"{where}: 프리팹 시간표에", errors);
            RequireTimeline(timelines, ActionKeys.Death, $"{where}: 프리팹 시간표에", errors);

            if (skills == null) continue;
            foreach (SkillEntry skill in skills.entries)
            {
                if (skill?.data == null) continue;
                RequireTimeline(timelines, skill.data.AnimationTrigger, $"{where}: 스킬 {skill.data.Id}({skill.data.SkillName})의 행동 키가 프리팹 시간표에", errors);
            }
        }
    }

    private static void ValidateStages(StageDatabase stages, MonsterDatabase monsters, List<string> errors)
    {
        if (stages == null) return;

        foreach (StageEntry entry in stages.entries)
        {
            StageData data = entry?.data;
            if (data == null) continue;
            string where = $"[CombatData] 스테이지 {data.StageNumber}";

            if (monsters == null || monsters.GetById(data.NormalMonsterId) == null) errors.Add($"{where}: 일반 몬스터 {data.NormalMonsterId}가 MonsterDatabase에 없습니다.");
            if (monsters == null || monsters.GetById(data.BossMonsterId) == null) errors.Add($"{where}: 보스 {data.BossMonsterId}가 MonsterDatabase에 없습니다.");
            if (data.MonstersPerEncounter <= 0) errors.Add($"{where}: monstersPerEncounter는 0보다 커야 합니다: {data.MonstersPerEncounter}");
            if (data.EncountersToComplete <= 0) errors.Add($"{where}: encountersToComplete는 0보다 커야 합니다: {data.EncountersToComplete}");
            if (!(data.SpawnDistance > 0f) || float.IsInfinity(data.SpawnDistance))
                errors.Add($"{where}: spawnDistance는 0보다 커야 합니다(플레이어 앞에 스폰): {data.SpawnDistance}");
            if (!(data.MonsterSpacing >= 0f)) errors.Add($"{where}: monsterSpacing은 0 이상이어야 합니다: {data.MonsterSpacing}");
            if (!(data.BossTimeLimit >= 0.0) || double.IsInfinity(data.BossTimeLimit))
                errors.Add($"{where}: bossTimeLimit은 0 이상이어야 합니다(0 = 제한 없음): {data.BossTimeLimit}");
            if (!System.Enum.IsDefined(typeof(StageClearType), data.ClearType))
                errors.Add($"{where}: clearType {(int)data.ClearType}은 처리하는 클리어 방식이 아닙니다(KillBoss만 있다).");
        }
    }

    // 스킬 이펙트 프리팹의 파티클은 로컬 시뮬레이션 공간이어야 한다. 원점 재설정 때 CombatEffectManager가 트랜스폼만
    // 옮기므로, 월드 공간 파티클은 날아가던 입자가 제자리에 남아 튄다(docs/COMBAT_ARCHITECTURE.md 9-3).
    private static void ValidateSkillEffects(SkillDatabase skills, List<string> errors)
    {
        if (skills == null) return;
        foreach (SkillEntry entry in skills.entries)
        {
            if (entry?.data == null) continue;
            RequireLocalParticles(entry.castVfxPrefab, $"[CombatData] 스킬 {entry.data.Id}({entry.data.SkillName})의 시전 이펙트", errors);
            RequireLocalParticles(entry.hitVfxPrefab, $"[CombatData] 스킬 {entry.data.Id}({entry.data.SkillName})의 타격 이펙트", errors);
        }
    }

    private static void RequireLocalParticles(GameObject prefab, string context, List<string> errors)
    {
        if (prefab == null) return;
        foreach (ParticleSystem ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World)
                errors.Add($"{context}({prefab.name}/{ps.name}): 파티클이 월드 시뮬레이션 공간입니다. 로컬(Local)로 바꾸세요.");
        }
    }

    private static void RequireTimeline(ActionTimelineSet timelines, string key, string context, List<string> errors)
    {
        if (timelines == null)
        {
            errors.Add($"{context} 쓸 시간표가 없습니다(ActionTimelineSet 미연결).");
            return;
        }
        if (string.IsNullOrEmpty(key) || !timelines.TryGet(key, out ActionTimeline timeline))
        {
            errors.Add($"{context} '{key}' 행동이 없습니다({timelines.name}).");
            return;
        }
        if (!timeline.IsValid) errors.Add($"{context} '{key}' 행동이 잘못됐습니다({timelines.name}): {timeline.ValidationError}");
    }

    private static T LoadFirst<T>() where T : Object
    {
        foreach (string guid in AssetDatabase.FindAssets("t:" + typeof(T).Name))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) return asset;
        }
        return null;
    }
}
