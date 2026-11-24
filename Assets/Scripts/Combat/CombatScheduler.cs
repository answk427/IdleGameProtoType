using System;
using System.Collections.Generic;
using UnityEngine;

// 한 고정 단계 [t0, t1] 안에서 교전 중인 모든 전투원의 사건을 시각 순서대로 처리한다(docs/COMBAT_ARCHITECTURE.md 7-11). 순수 C#.
//
//   지금 = t0에서 모두 결정
//   반복: t = 모든 전투원의 다음 사건 시각 중 최소. t > t1이면 끝
//         시계를 t로 옮긴다(대입이라 오차 없음)
//         ① 모든 전투원의 t 시각 표식을 처리(타격 등)
//         ② 모든 전투원이 t 시각에 결정 — 그 순간의 결과(누가 죽었는지)를 모두 본 뒤에 결정한다
//   시계를 t1로 옮긴다
//
// 같은 시각의 사건은 participants 순서로 처리한다(플레이어 → 몬스터 생성 순, 동시 사건 규칙).
// 사건 도중 죽은 전투원은 IsActive가 꺼져서 그 뒤 사건을 처리하지 않는다.
// 다음 사건 시각은 매번 전투원에게 다시 묻는다. 결정이 사건을 바꾸므로 큐를 따로 두면 낡은 항목이 생긴다(전투원이 몇 명뿐이라 비용도 작다).
public sealed class CombatScheduler
{
    // 전투원 한 명이 한 단계에 처리할 수 있는 사건 수의 상한. 행동 하나는 사건이 2개(타격, 완료) 남짓이라 초당 약 6000회 행동까지 정확하다.
    // 넘으면 그 단계의 나머지 사건은 다음 단계로 미루고 에러를 남긴다(무한 반복 방지).
    public const int MaxEventsPerParticipantPerStep = 256;

    private bool reportedCap;

    public void RunStep(SimClock clock, IReadOnlyList<CombatActions> participants)
    {
        if (clock == null) throw new ArgumentNullException(nameof(clock));
        if (participants == null) throw new ArgumentNullException(nameof(participants));

        double t = clock.Now;
        double end = clock.StepEnd;
        DecideAll(participants, t);

        int cap = Math.Max(1, participants.Count) * MaxEventsPerParticipantPerStep;
        for (int i = 0; ; i++)
        {
            double next = double.PositiveInfinity;
            for (int p = 0; p < participants.Count; p++)
            {
                if (participants[p].IsActive) next = Math.Min(next, participants[p].NextEventTime(t));
            }
            if (next > end) break;

            if (i >= cap)
            {
                if (!reportedCap)
                {
                    reportedCap = true;
                    Debug.LogError($"[CombatScheduler] 한 단계의 사건이 {cap}개를 넘었습니다. 행동 길이가 너무 짧습니다. 남은 사건은 다음 단계로 미룹니다.");
                }
                break;
            }

            if (next > t) t = next; // 사건 시각은 지금 이후다. 상한으로 미뤄진 사건만 지금 시각에 처리된다
            clock.SetNow(t);

            for (int p = 0; p < participants.Count; p++)
            {
                if (participants[p].IsActive) participants[p].FireEventsAt(t);
            }
            DecideAll(participants, t);
        }

        clock.SetNow(end);
    }

    private static void DecideAll(IReadOnlyList<CombatActions> participants, double t)
    {
        for (int p = 0; p < participants.Count; p++)
        {
            if (participants[p].IsActive) participants[p].DecideAt(t);
        }
    }
}
