using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

// 행동 시간표 굽기와 전투 데이터 검사(docs/COMBAT_ARCHITECTURE.md 7-2, 7-13).
//   - 굽기: 클립의 타격 표식 이벤트(AE_OnHit)를 ActionTimeline의 타격 비율로 저장한다.
//   - 검사: 시간표를 굽지 않았거나, 구운 뒤 클립이 바뀌었거나(오래된 굽기), 값이 규칙을 어기면 에러.
//     행동의 길이를 정하는 데이터(스킬 시전 시간)도 같이 본다.
//     에디터가 열릴 때(스크립트 다시 불러올 때마다) 로그로, 빌드 직전에는 빌드 실패로 알린다.
public static class ActionTimelineTools
{
    [MenuItem("IdleGame/Combat/Bake Action Timelines")]
    public static void BakeAllMenu()
    {
        int baked = BakeAll(out List<string> errors);
        foreach (string error in errors) Debug.LogError(error);
        Debug.Log($"[ActionTimeline] 굽기 완료: 시간표 {baked}개, 에러 {errors.Count}개");
    }

    public static int BakeAll(out List<string> errors)
    {
        errors = new List<string>();
        int baked = 0;

        foreach (ActionTimelineSet set in LoadAllSets())
        {
            var so = new SerializedObject(set);
            SerializedProperty list = so.FindProperty("timelines");

            for (int i = 0; i < list.arraySize; i++)
            {
                SerializedProperty element = list.GetArrayElementAtIndex(i);
                string key = element.FindPropertyRelative("key").stringValue;
                var clip = element.FindPropertyRelative("clip").objectReferenceValue as AnimationClip;
                string where = $"{AssetDatabase.GetAssetPath(set)} [{key}]";

                if (clip == null)
                {
                    errors.Add($"[ActionTimeline] {where}: 클립이 없습니다.");
                    continue;
                }

                List<ActionTimelineBaking.Marker> markers = ReadMarkers(clip);
                ActionTimelineBaking.Result result = ActionTimelineBaking.Bake(clip.length, markers);
                if (!result.Succeeded)
                {
                    errors.Add($"[ActionTimeline] {where}: {result.Error} (클립 {clip.name})");
                    continue;
                }

                string usageError = ActionTimelineBaking.ValidateUsage(key, result.HitFractions.Length);
                if (usageError != null)
                {
                    errors.Add($"[ActionTimeline] {where}: {usageError} (클립 {clip.name})");
                    continue;
                }

                element.FindPropertyRelative("clipLength").floatValue = clip.length;
                SerializedProperty hits = element.FindPropertyRelative("hitFractions");
                hits.arraySize = result.HitFractions.Length;
                for (int h = 0; h < result.HitFractions.Length; h++)
                {
                    hits.GetArrayElementAtIndex(h).floatValue = result.HitFractions[h];
                }
                element.FindPropertyRelative("bakedSignature").stringValue = ActionTimelineBaking.Signature(clip.length, markers);
                baked++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(set);
        }

        AssetDatabase.SaveAssets();
        return baked;
    }

    // 모든 세트를 검사해 문제 목록을 돌려준다. 비어 있으면 정상.
    public static List<string> ValidateAll()
    {
        var errors = new List<string>();

        foreach (ActionTimelineSet set in LoadAllSets())
        {
            string path = AssetDatabase.GetAssetPath(set);
            var keys = new HashSet<string>();

            foreach (ActionTimeline timeline in set.Timelines)
            {
                if (timeline == null) continue;
                string where = $"{path} [{timeline.Key}]";

                if (!keys.Add(timeline.Key ?? string.Empty)) errors.Add($"[ActionTimeline] {where}: 같은 키가 두 번 있습니다.");

                if (timeline.Clip == null)
                {
                    errors.Add($"[ActionTimeline] {where}: 클립이 없습니다.");
                    continue;
                }

                string current = ActionTimelineBaking.Signature(timeline.Clip.length, ReadMarkers(timeline.Clip));
                if (string.IsNullOrEmpty(timeline.BakedSignature))
                    errors.Add($"[ActionTimeline] {where}: 아직 굽지 않았습니다. IdleGame/Combat/Bake Action Timelines를 실행하세요.");
                else if (timeline.BakedSignature != current)
                    errors.Add($"[ActionTimeline] {where}: 클립({timeline.Clip.name})이 구운 뒤에 바뀌었습니다. 다시 구우세요.");

                string valueError = timeline.ValidationError;
                if (valueError != null) errors.Add($"[ActionTimeline] {where}: {valueError}");
            }
        }

        return errors;
    }

    // 스킬의 시전 시간(= 스킬 행동의 길이) 검사. 0 이하면 그 스킬은 런타임에 쓰이지 않고 에러만 남으므로 빌드 전에 드러낸다.
    // 엑셀에서 열이 빠지면 변환기가 거부하고, 값을 0으로 적은 경우는 여기서 잡힌다.
    public static List<string> ValidateSkills()
    {
        var errors = new List<string>();
        foreach (string guid in AssetDatabase.FindAssets("t:SkillDatabase"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var database = AssetDatabase.LoadAssetAtPath<SkillDatabase>(path);
            if (database == null) continue;

            foreach (SkillEntry entry in database.entries)
            {
                SkillData data = entry?.data;
                if (data == null) continue;
                if (!(data.CastTime > 0f) || float.IsInfinity(data.CastTime))
                    errors.Add($"[CombatData] {path} 스킬 {data.Id}({data.SkillName}): 시전 시간(castTime)은 0보다 커야 합니다: {data.CastTime}");
            }
        }
        return errors;
    }

    // 전투 데이터 전체 검사(행동 시간표, 스킬 시전 시간, 데이터와 프리팹의 짝). 비어 있으면 정상.
    public static List<string> ValidateCombatData()
    {
        List<string> errors = ValidateAll();
        errors.AddRange(ValidateSkills());
        errors.AddRange(CombatDataValidator.ValidateDatabases());
        return errors;
    }

    public static List<ActionTimelineBaking.Marker> ReadMarkers(AnimationClip clip)
    {
        var markers = new List<ActionTimelineBaking.Marker>();
        foreach (AnimationEvent e in AnimationUtility.GetAnimationEvents(clip))
        {
            markers.Add(new ActionTimelineBaking.Marker(e.functionName, e.time));
        }
        return markers;
    }

    private static IEnumerable<ActionTimelineSet> LoadAllSets()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:ActionTimelineSet"))
        {
            var set = AssetDatabase.LoadAssetAtPath<ActionTimelineSet>(AssetDatabase.GUIDToAssetPath(guid));
            if (set != null) yield return set;
        }
    }
}

// 에디터가 스크립트를 다시 불러올 때마다 검사해서 오래된 굽기를 바로 드러낸다.
[InitializeOnLoad]
internal static class ActionTimelineEditorCheck
{
    static ActionTimelineEditorCheck()
    {
        EditorApplication.delayCall += () =>
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            foreach (string error in ActionTimelineTools.ValidateCombatData()) Debug.LogError(error);
        };
    }
}

// 빌드 직전 검사. 문제가 있으면 빌드를 실패시킨다(숫자와 그림이 어긋나거나 쓸 수 없는 스킬이 있는 채로 나가지 않게).
internal sealed class ActionTimelineBuildCheck : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        List<string> errors = ActionTimelineTools.ValidateCombatData();
        if (errors.Count == 0) return;

        foreach (string error in errors) Debug.LogError(error);
        throw new BuildFailedException($"전투 데이터 검사 실패 {errors.Count}건. 시간표는 IdleGame/Combat/Bake Action Timelines로 다시 굽고, 스킬 시전 시간은 SkillDatabase에서 고치세요.");
    }
}
