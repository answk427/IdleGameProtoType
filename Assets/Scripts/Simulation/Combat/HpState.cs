using System;

// 전투원의 HP(현재, 최대). 호스트가 반응형 상태로 낸다(CombatantHost.Hp). 값은 GameNumber(D5).
public readonly struct HpState : IEquatable<HpState>
{
    public readonly GameNumber Current;
    public readonly GameNumber Max;

    public HpState(GameNumber current, GameNumber max)
    {
        Current = current;
        Max = max;
    }

    // 0~1. 최대가 0 이하면 0.
    public float Ratio => Max > GameNumber.Zero ? (float)(Current.ToDouble() / Max.ToDouble()) : 0f;

    public bool Equals(HpState other) => Current == other.Current && Max == other.Max;
    public override bool Equals(object obj) => obj is HpState other && Equals(other);
    public override int GetHashCode() => Current.GetHashCode() * 31 + Max.GetHashCode();
    public override string ToString() => $"{Current}/{Max}";
}
