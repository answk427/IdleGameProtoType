// 바깥(UI, 뷰, 개발자 도구)이 보는 흐름의 접점(docs/COMBAT_ARCHITECTURE.md 13-6, D52): 상태, 사건, 명령.
// 진행(Begin, Tick)은 주인(Game)과 루프만 한다. 명령은 검사·결과가 즉시이고 실행은 다음 ①이다(D23).
// 값과 사건은 처리 밖에서만 바뀐다(①이 끝난 뒤, 12절 D48).
public interface IEncounterFlow
{
    // 상태
    IReadOnlyReactiveProperty<FlowPhase> Phase { get; }
    // 0이면 아직 스테이지에 들어가지 않았다(Begin 전).
    IReadOnlyReactiveProperty<int> CurrentStageNumber { get; }
    IReadOnlyReactiveProperty<bool> BossChallengeAvailable { get; }

    // 사건
    Observable<StageEntered> StageEntered { get; }
    Observable<string> Stalled { get; }

    // 매 프레임 값의 원인(시각)
    TransitionTimes Transition { get; }
    double BossDeadline { get; }
    double Now { get; }

    // 지금 스테이지의 값(시작 전이면 null)과 이번 스테이지에서 깬 인카운터 수
    StageSpec Stage { get; }
    int EncountersCleared { get; }

    // 명령
    BossChallengeResult RequestBossChallenge();
    RestartResult RestartAt(int stageNumber);
}
