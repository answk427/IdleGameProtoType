using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Serialization;

// 게임(Game, 순수 C#)을 Unity에서 돌리는 실행기(docs/COMBAT_ARCHITECTURE.md 11절 5-B, 12절 D45). 씬에 하나만 둔다.
// - 화면 프레임마다: 흐른 게임 시간(Time.deltaTime, 배속 포함)만큼 게임을 진행하고(고정 단계는 SimulationLoop가 센다),
//   전투의 그림(BattleView)을 직전 두 단계 사이로 보간한다. 다른 Update보다 먼저 돈다(예전 FixedUpdate가 Update보다 먼저였던 순서).
//   카메라와 배경은 그 뒤(CameraRig.LateUpdate).
// - 저장: 게임이 저장할 때가 됐다고 알리면(SaveRequested) 저장소에 쓴다. 일시정지·종료 때는 그 자리에서 끝까지 쓴다.
// 전투 쪽에서 Time.deltaTime을 읽는 곳은 여기 한 곳이고, 루프의 입력일 뿐이다.
// (예전 CombatSimulation과 GameManager의 저장 부분. 파일과 GUID를 옮겨 씬의 참조가 그대로 남는다.)
[DefaultExecutionOrder(-500)]
public class GameRunner : MonoBehaviour
{
    [FormerlySerializedAs("monsterSpawner")]
    [SerializeField] private BattleView battleView;

    private readonly CompositeDisposable subscriptions = new CompositeDisposable();

    // 시작에 성공한 뒤에만 있다.
    public Game Game { get; private set; }
    public BattleView View => battleView;

    public bool ValidateReferences()
    {
        if (battleView != null) return true;
        Debug.LogError("[GameRunner] battleView가 연결되지 않았습니다.");
        return false;
    }

    // GameInitializer가 게임과 뷰를 이은 뒤 부른다. 이때부터 프레임마다 진행하고 저장한다.
    public void Run(Game game)
    {
        Game = game ?? throw new ArgumentNullException(nameof(game));
        game.SaveRequested.Subscribe(_ => SaveGame()).AddTo(subscriptions);
        game.Flow.StageEntered.Subscribe(e =>
        {
            if (e.Reason != StageEntryReason.Start) Debug.Log($"[GameRunner] 스테이지 {e.StageNumber} 입장({e.Reason})");
        }).AddTo(subscriptions);
        game.Flow.Stalled.Subscribe(message => Debug.LogWarning(message)).AddTo(subscriptions);
    }

    private void Update()
    {
        if (Game == null) return;

        // 흐른 게임 시간만큼 고정 단계를 돌린다(배속과 한 프레임 상한이 들어 있다). 단계 밖의 사건도 여기서 배달된다.
        Game.Advance(Time.deltaTime);
        battleView.Render(Game.Loop.Alpha); // 그려지는 위치는 실제 시뮬레이션보다 최대 한 단계 늦다
    }

    private void OnApplicationQuit()
    {
        // 곧 프로세스가 종료돼 StartCoroutine으로 예약한 저장이 실행될 프레임을
        // 못 받을 수 있다 — 끝까지 동기적으로 돌려서 저장이 실제로 끝나도록 한다.
        SaveGameImmediate();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus) SaveGameImmediate();
    }

    private void OnDestroy()
    {
        subscriptions.Dispose();
        Game?.Dispose();
    }

    // 게임을 시작하지 못했으면(세이브를 읽지 못함 등) 게임이 없어 아무것도 쓰지 않는다. 그래서 세이브 원본이 그대로 남는다.
    private void SaveGame()
    {
        IEnumerator routine = BuildSaveRoutine();
        if (routine != null) StartCoroutine(routine);
    }

    // 종료/일시정지처럼 다음 프레임을 보장 못 받는 시점 전용. 로컬 저장 기준으로는
    // 안전하지만, 나중에 서버 구현체로 바뀌면 응답을 못 기다리고 끊길 수 있다는
    // 한계가 있다 — 그건 그때 "종료 직전 저장을 어떻게 보장할지"를 다시 봐야 한다.
    private void SaveGameImmediate()
    {
        IEnumerator routine = BuildSaveRoutine();
        if (routine == null) return;
        while (routine.MoveNext()) { }
    }

    private IEnumerator BuildSaveRoutine() =>
        Game != null ? SaveStorageProvider.Current.Save(Game.CreateSaveData()) : null;
}
