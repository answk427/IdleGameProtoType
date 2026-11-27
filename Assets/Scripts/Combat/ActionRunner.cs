using System;

// 행동 하나의 표식(타격들, 완료)을 시각 순서대로 알린다(docs/COMBAT_ARCHITECTURE.md 7-3, 7-11, 7-13).
public interface IActionListener
{
    void OnActionHit(ActionTiming action, int hitIndex);
    void OnActionCompleted(ActionTiming action);
}

// 순수 C#. 시각은 시뮬레이션 시계의 절대 시각(초, double)이다.
// 시작할 때 모든 표식의 시각(시작 + 비율 × 길이, 끝 = 시작 + 길이)을 한 번 계산해 저장한다. 다시 계산하지 않으므로
// 스케줄러가 "다음 표식 시각"으로 시계를 옮긴 뒤 그 시각까지 처리하면, 그 표식이 정확히 처리된다(여유값 없음).
// 같은 시각의 표식은 타격 → 완료 순서다. 행동은 끝까지 진행되고, 도중에 멈추는 것은 사망 같은 강제 중단(Cancel)뿐이다.
// 알림을 받는 쪽은 표식 처리 도중에 Start/Cancel을 부르지 않는다(다음 행동은 처리가 끝난 뒤 결정한다).
public sealed class ActionRunner
{
    private ActionTiming current;
    private double startTime;
    private double duration;
    private double endTime;
    private double[] hitTimes = new double[4];
    private int hitCount;
    private int nextHit;
    private bool firing;

    public bool IsRunning => current != null;
    public ActionTiming Current => current;
    public double StartTime => startTime;
    public double Duration => duration;
    public double EndTime => endTime;

    // 다음에 처리할 표식의 시각. 행동이 없으면 무한대.
    public double NextMarkerTime
    {
        get
        {
            if (current == null) return double.PositiveInfinity;
            return nextHit < hitCount ? hitTimes[nextHit] : endTime;
        }
    }

    public void Start(ActionTiming action, double actionDuration, double atTime)
    {
        if (firing) throw new InvalidOperationException("표식 처리 도중에는 새 행동을 시작할 수 없습니다.");
        if (action == null) throw new ArgumentNullException(nameof(action));
        if (!(actionDuration > 0.0) || double.IsInfinity(actionDuration))
            throw new ArgumentOutOfRangeException(nameof(actionDuration), actionDuration, "행동 길이는 0보다 큰 유한한 값이어야 합니다.");
        if (double.IsNaN(atTime) || double.IsInfinity(atTime))
            throw new ArgumentOutOfRangeException(nameof(atTime), atTime, "시작 시각은 유한한 값이어야 합니다.");
        string error = action.ValidationError;
        if (error != null) throw new ArgumentException($"잘못된 시간표 '{action.Key}': {error}", nameof(action));

        current = action;
        startTime = atTime;
        duration = actionDuration;
        endTime = atTime + actionDuration;

        var fractions = action.HitFractions;
        hitCount = fractions.Count;
        if (hitTimes.Length < hitCount) hitTimes = new double[hitCount];
        for (int i = 0; i < hitCount; i++)
        {
            // 비율이 1이면 끝 시각과 정확히 같은 값이 되게 한다(곱셈 반올림으로 끝보다 늦어지지 않게).
            hitTimes[i] = fractions[i] >= 1f ? endTime : atTime + fractions[i] * actionDuration;
        }

        nextHit = 0;
    }

    // 강제 중단(사망, 교전 종료). 남은 표식은 처리하지 않는다.
    public void Cancel()
    {
        if (firing) throw new InvalidOperationException("표식 처리 도중에는 행동을 멈출 수 없습니다.");
        Clear();
    }

    // time까지(포함)의 표식을 시각 순서대로 처리한다. 완료되면 행동이 비워진다.
    public void FireMarkersUpTo(double time, IActionListener listener)
    {
        if (double.IsNaN(time)) throw new ArgumentOutOfRangeException(nameof(time));
        if (current == null) return;

        firing = true;
        try
        {
            ActionTiming action = current;
            while (nextHit < hitCount && hitTimes[nextHit] <= time)
            {
                // 증가를 호출 인자 안에 두지 않는다: listener?.M(i++)은 listener가 null이면 인자까지 건너뛰어 무한 반복이 된다.
                int hitIndex = nextHit;
                nextHit++;
                listener?.OnActionHit(action, hitIndex);
            }

            if (nextHit >= hitCount && endTime <= time)
            {
                Clear();
                listener?.OnActionCompleted(action);
            }
        }
        finally
        {
            firing = false;
        }
    }

    private void Clear()
    {
        current = null;
        hitCount = 0;
        nextHit = 0;
    }
}
