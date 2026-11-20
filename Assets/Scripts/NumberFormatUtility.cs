using System;
using System.Globalization;

// 큰 수를 K/M/B/T → aa, ab, … 단위로 줄여서 표시하는 공용 포맷터.
// 골드/경험치/업그레이드 비용처럼 무한히 커질 수 있는 값을 UI에 표시할 때 쓴다 —
// 그냥 숫자를 그대로 찍으면 자릿수가 늘어날수록 고정폭 UI 박스를 넘어가거나 줄바꿈된다.
// T(10^12) 다음부터는 방치형에서 흔히 쓰는 두 글자 알파벳 단위(aa = 10^15, ab = 10^18, …)를 쓴다.
public static class NumberFormatUtility
{
    private static readonly string[] NamedSuffixes = { "", "K", "M", "B", "T" };
    private const int LetterCount = 26;
    private const int MaxTier = 4 + LetterCount * LetterCount; // T 다음 aa ~ zz

    public static string Format(int value) => Format((double)value);

    public static string Format(GameNumber value) => Format(value.ToDouble());

    private static string Format(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
            return value.ToString(CultureInfo.InvariantCulture);

        if (value < 1000d)
            return value.ToString("0", CultureInfo.InvariantCulture);

        int tier = 1;
        double unit = 1000d;
        while (tier < MaxTier && value >= unit * 1000d)
        {
            unit *= 1000d;
            tier++;
        }

        // 예: 999,999는 999.999K로 반올림하면 "1000K"가 되는데, 단위를 못 넘어가서 잘못된 표기가 된다.
        // 반올림 결과가 1000을 넘으면 한 단계 위 단위로 다시 계산해서 "1M"으로 보이게 한다.
        double scaled = Math.Round(value / unit, 1, MidpointRounding.AwayFromZero);
        if (scaled >= 1000d && tier < MaxTier)
        {
            unit *= 1000d;
            tier++;
            scaled = Math.Round(value / unit, 1, MidpointRounding.AwayFromZero);
        }

        return scaled.ToString("0.#", CultureInfo.InvariantCulture) + GetSuffix(tier);
    }

    private static string GetSuffix(int tier)
    {
        if (tier < NamedSuffixes.Length) return NamedSuffixes[tier];

        int index = tier - NamedSuffixes.Length; // 0 = aa
        char first = (char)('a' + index / LetterCount);
        char second = (char)('a' + index % LetterCount);
        return new string(new[] { first, second });
    }
}
