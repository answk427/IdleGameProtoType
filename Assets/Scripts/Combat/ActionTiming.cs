using System;
using System.Collections.Generic;

// 행동 하나(기본 공격, 스킬, 사망)의 시간표 값(docs/COMBAT_ARCHITECTURE.md 7-2, 7-13, 13-4). 시뮬레이션의 입력이다. 순수 C#, 불변.
// 애니메이션이 정하는 것은 타격이 행동의 몇 % 지점인지(비율)와, 재생 속도 계산에 쓸 클립 길이뿐이다.
// 행동의 길이는 게임 데이터가 정한다: 기본 공격 = 공격 간격, 스킬 = SkillSpec.CastTime. 사망은 클립 길이.
// 데이터 쪽(GameData)이 에셋(ActionTimeline: 클립과 거기서 구운 값)에서 한 번 만든다. 시뮬레이션은 클립을 모른다.
// 값의 규칙은 여기 한 곳이다. 굽기, 에셋 검사, 테스트가 이것을 부른다.
public sealed class ActionTiming
{
    private readonly float[] hitFractions;

    // 검사하지 않고 만든다. 규칙에 어긋나면 ValidationError가 이유를 갖고, 행동으로 시작하지 않는다(에셋에서 바꿀 때).
    public ActionTiming(string key, float clipLength, IReadOnlyList<float> hitFractions)
    {
        Key = key;
        ClipLength = clipLength;
        this.hitFractions = Copy(hitFractions);
        ValidationError = Validate(key, clipLength, this.hitFractions);
    }

    // 행동 키이자 애니메이터 상태 이름(Attack, Skill, Healing, Death)
    public string Key { get; }
    public float ClipLength { get; }
    public IReadOnlyList<float> HitFractions => hitFractions;

    // 규칙에 어긋나는 이유. 맞으면 null.
    public string ValidationError { get; }
    public bool IsValid => ValidationError == null;

    // 값으로 만든다(코드와 테스트). 규칙에 어긋나면 예외.
    public static ActionTiming FromValues(string key, float clipLength, float[] hitFractions)
    {
        var timing = new ActionTiming(key, clipLength, hitFractions);
        if (timing.ValidationError != null) throw new ArgumentException($"잘못된 시간표 '{key}': {timing.ValidationError}");
        return timing;
    }

    // 값 규칙 전부(범위, 키에 따른 사용).
    public static string Validate(string key, float clipLength, IReadOnlyList<float> hitFractions) =>
        ValidateValues(clipLength, hitFractions ?? Array.Empty<float>()) ?? ValidateUsage(key, hitFractions?.Count ?? 0);

    // 키에 따른 사용 규칙. 공격과 스킬은 효과를 낼 타격이 있어야 하고, 사망은 타격이 없어야 한다.
    public static string ValidateUsage(string key, int hitCount)
    {
        if (string.IsNullOrEmpty(key)) return "키가 비어 있습니다";
        if (key == ActionKeys.Death) return hitCount == 0 ? null : "사망 시간표에는 타격 표식이 없어야 합니다";
        return hitCount > 0 ? null : $"'{key}' 시간표에 타격 표식(AE_OnHit)이 없습니다";
    }

    // 값의 범위 규칙. 굽기 결과와 코드로 만든 시간표가 같은 규칙을 지킨다.
    public static string ValidateValues(float clipLength, IReadOnlyList<float> hitFractions)
    {
        if (!IsPositiveFinite(clipLength)) return $"클립 길이가 0보다 커야 합니다: {clipLength}";
        float previous = 0f;
        for (int i = 0; i < hitFractions.Count; i++)
        {
            float f = hitFractions[i];
            if (!(f > 0f) || f > 1f) return $"타격 비율은 (0, 1] 안이어야 합니다: {f}";
            if (f < previous) return "타격 비율은 오름차순이어야 합니다";
            previous = f;
        }
        return null;
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

    private static float[] Copy(IReadOnlyList<float> source)
    {
        if (source == null || source.Count == 0) return Array.Empty<float>();
        var copy = new float[source.Count];
        for (int i = 0; i < copy.Length; i++) copy[i] = source[i];
        return copy;
    }
}

// 전투원 하나의 행동 시간표 목록(키 → ActionTiming). 데이터 쪽이 에셋(ActionTimelineSet)마다 한 번 만든다. 불변.
// 타이밍은 스킬이 아니라 시전자의 애니메이션에 속한다: 같은 스킬도 시전자마다 다른 시간표를 쓴다.
public sealed class ActionTimingSet
{
    private readonly ActionTiming[] timings;

    public ActionTimingSet(IReadOnlyList<ActionTiming> timings)
    {
        var list = new List<ActionTiming>(timings?.Count ?? 0);
        if (timings != null)
        {
            for (int i = 0; i < timings.Count; i++)
            {
                if (timings[i] != null) list.Add(timings[i]);
            }
        }
        this.timings = list.ToArray();
    }

    public static ActionTimingSet Of(params ActionTiming[] timings) => new ActionTimingSet(timings);

    public IReadOnlyList<ActionTiming> Timings => timings;

    // 같은 키가 둘이면 앞의 것(에셋 검사가 같은 키를 막는다).
    public bool TryGet(string key, out ActionTiming timing)
    {
        for (int i = 0; i < timings.Length; i++)
        {
            if (timings[i].Key == key)
            {
                timing = timings[i];
                return true;
            }
        }

        timing = null;
        return false;
    }
}
