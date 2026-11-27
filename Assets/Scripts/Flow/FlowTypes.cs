using System;

// 흐름(인카운터, 보스, 스테이지)의 작은 타입들(docs/COMBAT_ARCHITECTURE.md 10-3).

// 흐름의 단계 하나. 전투원의 CombatantPhase처럼 상태는 이 값 하나다.
public enum FlowPhase
{
    NotStarted,       // Begin 전
    Starting,         // 스테이지에 들어왔다. 다음 ①에서 첫 인카운터를 스폰한다(게임 시작)
    Encounter,        // 인카운터 몬스터가 나와 있다
    EncounterCleared, // 살아 있는 적이 없다. 시체를 남긴 채 정리 대기(encounterClearDelay)
    BossIntro,        // 보스 등장 대기(bossIntroDelay)
    Boss,             // 보스전
    Transition,       // 스테이지 전환(transitionDuration). 가운데에서 스테이지에 들어간다
}

// 스테이지에 들어간 이유
public enum StageEntryReason
{
    Start,     // 게임 시작(저장하지 않는다)
    Cleared,   // 보스 처치: 다음 스테이지(마지막이면 같은 스테이지)
    Defeated,  // 아군 전멸 또는 보스전 제한 시간 초과: 같은 스테이지 처음부터
    Requested, // RestartAt 명령(개발자 도구의 이동, 진행도 교체)
}

public readonly struct StageEntered : IEquatable<StageEntered>
{
    public readonly int StageNumber;
    public readonly StageEntryReason Reason;

    public StageEntered(int stageNumber, StageEntryReason reason)
    {
        StageNumber = stageNumber;
        Reason = reason;
    }

    public bool Equals(StageEntered other) => StageNumber == other.StageNumber && Reason == other.Reason;
    public override bool Equals(object obj) => obj is StageEntered other && Equals(other);
    public override int GetHashCode() => StageNumber * 31 + (int)Reason;
    public override string ToString() => $"스테이지 {StageNumber}({Reason})";
}

public enum BossChallengeResult
{
    Accepted,
    NotAvailable, // 도전 가능 상태가 아니다(인카운터를 덜 깼거나, 이미 받았다)
    Busy,         // 지금 단계에서는 받을 수 없다(보스전, 전환 중 등)
}

public enum RestartResult
{
    Accepted,
    Busy,        // 전환 중이거나 이미 받은 재시작이 있다(게임 시작 전 포함)
    NoSuchStage,
}

// 흐름의 대기 시간(초). 데이터(BalanceConfig, D24)에서 GameData.FlowTimings가 만든다. 0이면 그 대기가 없다(같은 ①에서 다음 단계로).
public readonly struct FlowTimings
{
    public readonly double EncounterClearDelay;
    public readonly double BossIntroDelay;
    public readonly double TransitionDuration;

    public FlowTimings(double encounterClearDelay, double bossIntroDelay, double transitionDuration)
    {
        EncounterClearDelay = encounterClearDelay;
        BossIntroDelay = bossIntroDelay;
        TransitionDuration = transitionDuration;
    }
}

// 전환의 시각(절대 시각, 초). 페이드가 이것과 시계로 그린다(매 프레임 값의 원인은 상태로, docs/UI_DESIGN.md 5-3).
public readonly struct TransitionTimes
{
    public readonly double Start;
    public readonly double Middle; // 스테이지에 들어가는 시각
    public readonly double End;

    public TransitionTimes(double start, double middle, double end)
    {
        Start = start;
        Middle = middle;
        End = end;
    }
}
