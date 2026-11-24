using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

// 클립에 찍힌 표식 이벤트를 행동 시간표 값(길이 대비 비율)으로 바꾸는 순수 함수와 그 검사 규칙
// (docs/COMBAT_ARCHITECTURE.md 7-2, 7-13). 에디터의 굽기 도구, 오래된 굽기 검사, 테스트가 모두 이것을 쓴다.
//
// 애니메이션이 정하는 것은 "비율"뿐이다. 행동의 길이는 게임 데이터가 정한다(기본 공격 = 공격 간격, 스킬 = 시전 시간).
// 표식은 AE_OnHit(타격, 여러 개 가능) 하나뿐이다. 이름이 AE_로 시작하는데 AE_OnHit이 아니면 오타나 옛 표식으로 보고 굽기를 거부한다.
// AE_로 시작하지 않는 이벤트(소리 등)는 무시한다.
public static class ActionTimelineBaking
{
    public const string HitMarker = "AE_OnHit";
    public const string MarkerPrefix = "AE_";

    public readonly struct Marker
    {
        public readonly string Name;
        public readonly float Time;

        public Marker(string name, float time)
        {
            Name = name;
            Time = time;
        }
    }

    public readonly struct Result
    {
        public readonly string Error;
        public readonly float[] HitFractions;

        public bool Succeeded => Error == null;

        public Result(string error, float[] hitFractions)
        {
            Error = error;
            HitFractions = hitFractions;
        }
    }

    public static Result Bake(float clipLength, IReadOnlyList<Marker> markers)
    {
        if (!IsPositiveFinite(clipLength)) return Fail($"클립 길이가 0보다 커야 합니다: {clipLength}");

        var hitTimes = new List<float>();
        foreach (Marker marker in markers)
        {
            string name = marker.Name ?? string.Empty;
            if (name == HitMarker) hitTimes.Add(marker.Time);
            else if (name.StartsWith(MarkerPrefix, StringComparison.Ordinal)) return Fail($"모르는 표식: {name} (표식은 {HitMarker}뿐)");
        }

        hitTimes.Sort();
        var hitFractions = new float[hitTimes.Count];
        for (int i = 0; i < hitTimes.Count; i++)
        {
            float t = hitTimes[i];
            if (!(t > 0f) || t > clipLength) return Fail($"타격 시점은 클립 범위 (0, {clipLength}] 안이어야 합니다: {t}");
            hitFractions[i] = t / clipLength;
        }

        return new Result(null, hitFractions);
    }

    // 키에 따른 사용 규칙. 공격과 스킬은 효과를 낼 타격이 있어야 하고, 사망은 타격이 없어야 한다.
    public static string ValidateUsage(string key, int hitCount)
    {
        if (string.IsNullOrEmpty(key)) return "키가 비어 있습니다";
        if (key == ActionKeys.Death) return hitCount == 0 ? null : "사망 시간표에는 타격 표식이 없어야 합니다";
        return hitCount > 0 ? null : $"'{key}' 시간표에 타격 표식(AE_OnHit)이 없습니다";
    }

    // 시간표 값의 범위 규칙. 굽기 결과와 코드로 만든 시간표가 같은 규칙을 지킨다.
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

    // 구울 때의 클립 길이와 표식을 문자열로 남긴다. 현재 클립의 서명과 다르면 다시 구워야 한다(오래된 굽기).
    public static string Signature(float clipLength, IReadOnlyList<Marker> markers)
    {
        var sorted = new List<Marker>();
        foreach (Marker marker in markers)
        {
            if (marker.Name != null && marker.Name.StartsWith(MarkerPrefix, StringComparison.Ordinal)) sorted.Add(marker);
        }
        sorted.Sort((a, b) => a.Time != b.Time ? a.Time.CompareTo(b.Time) : string.CompareOrdinal(a.Name, b.Name));

        var sb = new StringBuilder();
        sb.Append(clipLength.ToString("R", CultureInfo.InvariantCulture));
        foreach (Marker marker in sorted)
        {
            sb.Append('|').Append(marker.Name).Append('@').Append(marker.Time.ToString("R", CultureInfo.InvariantCulture));
        }
        return sb.ToString();
    }

    private static bool IsPositiveFinite(float value) => value > 0f && !float.IsInfinity(value);

    private static Result Fail(string error) => new Result(error, null);
}
