using System;

// 전투 시뮬레이션의 시계(docs/COMBAT_ARCHITECTURE.md 7-11). 순수 C#.
//
// 전투의 모든 시각은 이 시계의 절대 시각(초, double)이다. 시간을 갖는 상태는 남은 시간이 아니라 절대 시각으로 저장한다
// (행동의 표식 시각, 스킬의 준비 시각). 시각은 한 번 계산해 저장하고 다시 계산하지 않으므로, 같은 시각끼리 비교하면 정확히 같다.
// 단계 경계는 "단계 번호 × 단계 길이"로 계산해서 더해 가며 생기는 오차가 없다.
//
// Now는 지금 처리 중인 시각이다. 전투 사건을 처리하는 동안은 사건 시각이고, 그 밖에서는 단계 끝(= 다음 단계 시작)이다.
// Now를 앞으로 옮기는 것은 CombatScheduler뿐이고, 거꾸로 가거나 이번 단계 밖으로 나갈 수 없다.
public sealed class SimClock
{
    public SimClock(double stepLength)
    {
        if (!(stepLength > 0.0) || double.IsInfinity(stepLength))
            throw new ArgumentOutOfRangeException(nameof(stepLength), stepLength, "단계 길이는 0보다 큰 유한한 값이어야 합니다.");
        StepLength = stepLength;
    }

    public double StepLength { get; }
    public long StepIndex { get; private set; }
    public double StepStart => StepIndex * StepLength;
    public double StepEnd => (StepIndex + 1) * StepLength;
    public double Now { get; private set; }

    public void SetNow(double time)
    {
        if (time < Now || time > StepEnd)
            throw new ArgumentOutOfRangeException(nameof(time), time, $"시각은 거꾸로 가거나 이번 단계({StepStart}~{StepEnd}) 밖으로 나갈 수 없습니다. 지금 {Now}.");
        Now = time;
    }

    // 단계를 마치고 다음 단계로 넘어간다. Now는 새 단계의 시작(= 방금 단계의 끝)이 된다.
    public void AdvanceStep()
    {
        StepIndex++;
        Now = StepStart;
    }
}
