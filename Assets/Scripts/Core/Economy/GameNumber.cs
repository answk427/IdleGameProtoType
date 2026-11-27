using System;
using System.Globalization;

// 재화(골드, 업그레이드 비용, 처치 보상)처럼 상한 없이 지수적으로 커지는 수치를 감싸는 값 타입.
//
// 핵심은 "표현"이 아니라 "감싸는 것"이다. 바깥 API에는 GameNumber라는 이름만 노출되므로,
// 나중에 double(약 10^308)로 부족해져 "가수 + 지수" 방식으로 바꿔도 이 구조체 내부만 바뀐다.
//
// 저장 데이터(세이브 DTO, 데이터 테이블)에는 이 타입을 넣지 않는다. Unity/JsonUtility 직렬화는
// 필드 모양으로 매칭하므로, 구조체를 넣으면 기존 값이 조용히 사라진다 — 경계에서 ToDouble()/FromDouble()로 바꾼다.
//
// 한계: double이라 약 9×10^15을 넘으면 1 단위 정확도가 사라진다. 방치형 표시("1.2aa")와
// 구매 가능 비교에는 문제가 없는 수준으로 판단.
public readonly struct GameNumber : IEquatable<GameNumber>, IComparable<GameNumber>, IComparable
{
    private readonly double value;

    private GameNumber(double value)
    {
        this.value = value;
    }

    public static readonly GameNumber Zero = new GameNumber(0d);

    public static GameNumber FromDouble(double value) => new GameNumber(value);

    // 비용/보상처럼 정수여야 하는 값을 계산식(배율, 거듭제곱)으로 만들 때 쓴다.
    // Mathf.RoundToInt와 같은 반올림 규칙(짝수 쪽으로)을 쓴다.
    public static GameNumber Round(double value) => new GameNumber(Math.Round(value, MidpointRounding.ToEven));

    public double ToDouble() => value;

    // 정수 리터럴(0, 100 등)을 그대로 쓸 수 있게 한다. 정수 → GameNumber는 정보 손실이 없다.
    public static implicit operator GameNumber(int value) => new GameNumber(value);
    public static implicit operator GameNumber(long value) => new GameNumber(value);

    public static GameNumber operator +(GameNumber a, GameNumber b) => new GameNumber(a.value + b.value);
    public static GameNumber operator -(GameNumber a, GameNumber b) => new GameNumber(a.value - b.value);
    public static GameNumber operator *(GameNumber a, double multiplier) => new GameNumber(a.value * multiplier);
    public static GameNumber operator /(GameNumber a, double divisor) => new GameNumber(a.value / divisor);

    public static bool operator ==(GameNumber a, GameNumber b) => a.value == b.value;
    public static bool operator !=(GameNumber a, GameNumber b) => a.value != b.value;
    public static bool operator <(GameNumber a, GameNumber b) => a.value < b.value;
    public static bool operator >(GameNumber a, GameNumber b) => a.value > b.value;
    public static bool operator <=(GameNumber a, GameNumber b) => a.value <= b.value;
    public static bool operator >=(GameNumber a, GameNumber b) => a.value >= b.value;

    public static GameNumber Max(GameNumber a, GameNumber b) => a.value >= b.value ? a : b;
    public static GameNumber Min(GameNumber a, GameNumber b) => a.value <= b.value ? a : b;

    // 반응형 값의 "같은 값이면 알리지 않기"가 이 비교를 쓴다.
    public bool Equals(GameNumber other) => value.Equals(other.value);
    public override bool Equals(object obj) => obj is GameNumber other && Equals(other);
    public override int GetHashCode() => value.GetHashCode();

    public int CompareTo(GameNumber other) => value.CompareTo(other.value);

    public int CompareTo(object obj)
    {
        if (obj is GameNumber other) return CompareTo(other);
        throw new ArgumentException($"{nameof(GameNumber)}끼리만 비교할 수 있습니다.", nameof(obj));
    }

    // 디버그/로그용 원시 표기. 화면 표시는 NumberFormatUtility.Format()을 쓴다.
    public override string ToString() => value.ToString("R", CultureInfo.InvariantCulture);
}
