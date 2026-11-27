using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

// 게임 시작 시 객체를 순서대로 만들고 서로 연결하는 조립 지점(Composition Root). 씬에 하나만 둔다.
//
//   0. 창 시스템 준비(UIRoot.Prepare). 시작에 실패해도 안내창을 띄울 수 있게 항상 먼저 한다.
//   1. 데이터 한 벌(GameDataAssets → GameData + Presentation, docs/COMBAT_ARCHITECTURE.md 12절 D46)
//   2. 세이브 로드 (읽지 못하면 세이브 오류 창을 띄우고 중단)
//   3. 게임 생성(Game.TryCreate: 진행도 + 전투 + 흐름). 저장된 스테이지가 데이터에 없으면 같은 창을 띄우고 중단
//   4. 뷰(BattleView, 이펙트, 배경)와 UI가 게임을 구독, 실행기(GameRunner)가 프레임마다 돌린다
//   5. 흐름 시작(game.Begin). 모든 구독이 연결된 뒤라 처음 값(스테이지 번호, 플레이어 등장 등)이 그대로 전달된다.
//
// 실패는 게임을 만들기 전에 판정한다. 세이브를 지키려고 멈추는 경우(2, 3)는 사용자에게 안내하고, 참조·데이터 누락은
// 개발 오류라 콘솔 에러만 낸다. 어느 쪽이든 게임이 없으므로 저장 경로(종료·일시정지 저장 포함)가 아무것도 쓰지 않아
// 세이브 파일이 그대로 남는다.
//
// Awake가 아니라 Start에서 하는 이유: 각 객체의 Awake(자기 자신 초기화, 싱글턴 등록, PlayAreaBounds 계산 등)가
// 모두 끝난 뒤에 연결해야 하기 때문이다. "Awake = 자기 자신, Start = 다른 객체와 연결", DefaultExecutionOrder로 다른 Start보다 먼저 돈다.
[DefaultExecutionOrder(-1000)]
public class GameInitializer : MonoBehaviour
{
    [Header("데이터 (표 전부를 가리키는 에셋 하나)")]
    [SerializeField] private GameDataAssets gameData;

    [Header("플레이어")]
    [Tooltip("캐릭터 표(CharacterDatabase)의 id")]
    [SerializeField] private int characterId = 1;

    [Header("씬 참조")]
    [FormerlySerializedAs("combatSimulation")]
    [SerializeField] private GameRunner runner;
    [SerializeField] private BackgroundScroller background;
    [SerializeField] private UIRoot uiRoot;
    [Tooltip("전투 연출(데미지 숫자, 이펙트, 동전)")]
    [SerializeField] private CombatEffectManager effects;
    [Tooltip("GameObject 풀(전투원 그림, 이펙트)")]
    [SerializeField] private PoolManager pools;
    [Tooltip("바닥 높이(하단 UI 비율 기준)")]
    [SerializeField] private PlayAreaBounds playArea;
    [Tooltip("대열의 앞장을 따라가는 카메라(Main Camera)")]
    [SerializeField] private CameraRig cameraRig;

    // 조립이 끝난 뒤에만 있다(시작에 실패하면 null). 조립 지점이 모르는 쪽(개발 도구, docs/DEV_TOOLS.md T2)이 읽는다.
    public Game Game { get; private set; }
    public bool IsInitialized => Game != null;
    public BattleView BattleView => runner != null ? runner.View : null;
    public UIRoot UIRoot => uiRoot;
    public PlayAreaBounds PlayArea => playArea;

    // 조립이 끝났을 때 한 번 알린다. 시작에 실패하면 알리지 않는다.
    public event System.Action<GameInitializer> Initialized;

    private void Start()
    {
        // 참조가 빠지면(인스펙터 연결 누락) 에러 없이 null로 진행되다가 엉뚱한 곳에서 터진다.
        // 시작 지점에서 먼저 검사해 원인을 바로 보이게 한다.
        if (!ValidateReferences()) return;

        // 실패 때만 쓰는 조립 경로를 따로 두지 않는다. 창 시스템은 성공과 실패 모두 이 한 번으로 준비된다.
        uiRoot.Prepare();

        // 에디터 플레이에서는 튜닝 덮어쓰기를 적용한 사본을 쓴다(빌드에서는 원본 그대로, docs/COMBAT_ARCHITECTURE.md 8-7).
        var errors = new List<string>();
        GameTables tables = gameData.Prepare(errors);
        if (tables == null)
        {
            foreach (string error in errors) Debug.LogError($"[GameInitializer] {error}");
            return;
        }
        var data = new GameData(tables);
        var presentation = new Presentation(tables);

        PlayerSaveData saveData;
        try
        {
            saveData = SaveStorageProvider.Current.Load();
        }
        catch (System.Exception e)
        {
            // 새 데이터로 대신 시작하지 않는다. 게임이 없으면 저장 경로(종료 저장 포함)가 아무것도 쓰지 않으므로
            // 원본이 그대로 남는다. 파일 경로는 로그에만 남긴다(창에는 내지 않는다).
            Debug.LogError($"[GameInitializer] 세이브를 읽지 못해 게임을 시작하지 않습니다(원본 보존).\n{e}");
            uiRoot.ShowSaveError(SaveErrorArgs.Unreadable(e));
            return;
        }

        var options = new GameOptions(characterId, SimClock.StepLengthFromSetting(Time.fixedDeltaTime), Time.maximumDeltaTime); // 한 프레임 상한 = Maximum Allowed Timestep
        GameStart start = Game.TryCreate(data, saveData, options);
        switch (start.Error)
        {
            case GameStartError.StageMissing:
                Debug.LogError($"[GameInitializer] 저장된 스테이지 {start.SavedStageNumber}가 StageDatabase에 없어 게임을 시작하지 않습니다(세이브 보존).");
                uiRoot.ShowSaveError(SaveErrorArgs.StageMissing(start.SavedStageNumber));
                return;
            case GameStartError.CharacterMissing:
                Debug.LogError($"[GameInitializer] 캐릭터 표에 id {characterId}가 없거나 그 행동 시간표가 연결되지 않았습니다.");
                return;
        }

        Game game = start.Game;
#if UNITY_EDITOR
        // 플레이 중에 튜닝 덮어쓰기가 바뀌면 진행도의 파생 상태(전투 능력치, 필요 경험치, 레드닷 조건, 해금)를 다시 계산한다.
        DataTuning.Changed += game.RecomputeFromData;
#endif

        // 뷰와 UI가 게임을 구독한다. 흐름을 시작하기 전에 잇는다(첫 사건부터 받는다).
        runner.View.Bind(game, presentation, pools, playArea);
        effects.Bind(game.World, runner.View.Views, presentation, pools);
        cameraRig.Bind(game.World, runner.View.Views);
        background.Bind(game.Flow.CurrentStageNumber, presentation);
        uiRoot.BindGame(game, presentation);
        runner.Run(game);

        game.Begin();

        Game = game;
        Initialized?.Invoke(this);
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (Game != null) DataTuning.Changed -= Game.RecomputeFromData;
#endif
    }

    private bool ValidateReferences()
    {
        bool ok = true;
        if (gameData == null) { Debug.LogError("[GameInitializer] gameData(GameDataAssets)가 연결되지 않았습니다."); ok = false; }
        if (background == null) { Debug.LogError("[GameInitializer] background(BackgroundScroller)가 연결되지 않았습니다."); ok = false; }
        if (uiRoot == null) { Debug.LogError("[GameInitializer] uiRoot가 연결되지 않았습니다."); ok = false; }
        if (effects == null) { Debug.LogError("[GameInitializer] effects(CombatEffectManager)가 연결되지 않았습니다."); ok = false; }
        if (pools == null) { Debug.LogError("[GameInitializer] pools(PoolManager)가 연결되지 않았습니다."); ok = false; }
        if (playArea == null) { Debug.LogError("[GameInitializer] playArea(PlayAreaBounds)가 연결되지 않았습니다."); ok = false; }
        if (cameraRig == null) { Debug.LogError("[GameInitializer] cameraRig(CameraRig)가 연결되지 않았습니다."); ok = false; }
        if (runner == null) { Debug.LogError("[GameInitializer] runner(GameRunner)가 연결되지 않았습니다."); ok = false; }
        else if (!runner.ValidateReferences()) ok = false;
        return ok;
    }
}
