using System;
using System.Globalization;

// 큰 수를 K/M/B 단위로 줄여서 표시하는 공용 포맷터.
// 골드/경험치/업그레이드 비용처럼 무한히 커질 수 있는 값을 UI에 표시할 때 쓴다 —
// 그냥 숫자를 그대로 찍으면 자릿수가 늘어날수록 고정폭 UI 박스를 넘어가거나 줄바꿈된다.
public static class NumberFormatUtility
{
    public static string Format(int value)
    {
        if (value >= 1_000_000_000) return Scale(value, 1_000_000_000, "B", null);
        if (value >= 1_000_000) return Scale(value, 1_000_000, "M", "B");
        if (value >= 1_000) return Scale(value, 1_000, "K", "M");
        return value.ToString(CultureInfo.InvariantCulture);
    }

    // 예: 999,999는 999.999K로 반올림하면 "1000K"가 되는데, 단위를 못 넘어가서 잘못된 표기가 된다.
    // 반올림 결과가 1000을 넘으면 한 단계 위 단위로 다시 계산해서 "1M"으로 보이게 한다.
    private static string Scale(int value, int unit, string suffix, string nextSuffix)
    {
        double scaled = Math.Round(value / (double)unit, 1, MidpointRounding.AwayFromZero);
        if (nextSuffix != null && scaled >= 1000d)
            return Scale(value, unit * 1000, nextSuffix, null);
        return scaled.ToString("0.#", CultureInfo.InvariantCulture) + suffix;
    }
}
