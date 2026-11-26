using UnityEngine;

// 게임 시작 시 객체를 순서대로 만들고 서로 연결하는 조립 지점(Composition Root). 씬에 하나만 둔다.
//
//   0. 창 시스템 준비(UIRoot.Prepare). 시작에 실패해도 안내창을 띄울 수 있게 항상 먼저 한다.
//   1. 세이브 로드 (읽지 못하면 세이브 오류 창을 띄우고 중단)
//   2. GameSession 생성 + 세이브 적용(Apply), 저장된 스테이지 확인 (데이터에 없으면 같은 창을 띄우고 중단)
//   3. 플레이어 캐릭터 스폰 + 진행도 주입
//   4. CombatSimulation: 전투 월드와 흐름(EncounterDirector) 생성
//   5. GameManager(저장, 보상), 배경, UIRoot.BindGame이 흐름과 진행도를 구독
//   6. 흐름 시작(director.Begin). 모든 구독이 연결된 뒤라 처음 값(스테이지 번호 등)이 반응형으로 전달되므로
//      예전의 "게임 시작" 사건(OnGameStarted)이 필요 없다(docs/COMBAT_ARCHITECTURE.md 10-5).
//
// 실패는 월드를 만들기 전에 판정한다. 세이브를 지키려고 멈추는 경우(1, 2)는 사용자에게 안내하고, 참조 누락과
// 플레이어 생성 실패는 개발 오류라 콘솔 에러만 낸다. 어느 쪽이든 GameManager를 초기화하지 않으므로
// 저장 경로(종료·일시정지 저장 포함)가 아무것도 쓰지 않아 세이브 파일이 그대로 남는다.
//
// 예전에는 이 순서가 여러 Awake/Start에 흩어진 암묵 규칙이었다(캐릭터 Awake가 세이브를 읽고,
// GameManager.Start가 그 캐릭터에서 골드/스테이지를 꺼내 썼다). 이제 순서를 이 한 곳에서 읽을 수 있다.
//
// Awake가 아니라 Start에서 하는 이유: 각 객체의 Awake(자기 자신 초기화, 이벤트 구독, 싱글턴 등록,
// PlayAreaBounds 계산 등)가 모두 끝난 뒤에 연결해야 하기 때문이다. Unity 규약대로
// "Awake = 자기 자신, Start = 다른 객체와 연결"로 나누고, DefaultExecutionOrder로 다른 Start보다 먼저 돈다.
[DefaultExecutionOrder(-1000)]
public class GameInitializer : MonoBehaviour
{
    [Header("밸런스 데이터 (예전에는 플레이어 프리팹에 있던 참조)")]
    [SerializeField] private PlayerStatDatabase statData;
    [SerializeField] private PlayerUpgradeConfig upgradeConfig;

    [Header("씬 참조")]
    [SerializeField] private PlayerSpawner playerSpawner;
    [SerializeField] private GameManager gameManager;
    [SerializeField] private CombatSimulation combatSimulation;
    [SerializeField] private BackgroundScroller background;
    [SerializeField] private UIRoot uiRoot;

    // 조립이 끝난 뒤에만 있다(시작에 실패하면 null).
    public GameSession Session { get; private set; }

    // 조립이 끝난 뒤에만 의미가 있다(IsInitialized). 조립 지점이 모르는 쪽(개발 도구, docs/DEV_TOOLS.md T2)이 읽는다.
    public bool IsInitialized { get; private set; }
    public PlayerController Player { get; private set; }
    public GameManager GameManager => gameManager;
    public CombatSimulation CombatSimulation => combatSimulation;
    public UIRoot UIRoot => uiRoot;

    // 조립이 끝났을 때 한 번 알린다. 시작에 실패하면 알리지 않는다.
    public event System.Action<GameInitializer> Initialized;

    private void Start()
    {
        // 참조가 빠지면(인스펙터 연결 누락) 에러 없이 null로 진행되다가 엉뚱한 곳에서 터진다.
        // 시작 지점에서 먼저 검사해 원인을 바로 보이게 한다.
        if (!ValidateReferences()) return;

        // 실패 때만 쓰는 조립 경로를 따로 두지 않는다. 창 시스템은 성공과 실패 모두 이 한 번으로 준비된다.
        // UIRoot의 참조가 빠졌으면 false(에러 로그)인데, 지금처럼 게임은 UI 없이 시작한다.
        uiRoot.Prepare();

        PlayerSaveData saveData;
        try
        {
            saveData = SaveStorageProvider.Current.Load();
        }
        catch (System.Exception e)
        {
            // 새 데이터로 대신 시작하지 않는다. 세션이 없으면 GameManager의 저장 경로(종료 저장 포함)가
            // 아무것도 쓰지 않으므로 원본이 그대로 남는다. 파일 경로는 로그에만 남긴다(창에는 내지 않는다).
            Debug.LogError($"[GameInitializer] 세이브를 읽지 못해 게임을 시작하지 않습니다(원본 보존).\n{e}");
            uiRoot.ShowSaveError(SaveErrorArgs.Unreadable(e));
            return;
        }

        // 에디터 플레이에서는 튜닝 덮어쓰기를 적용한 사본을 쓴다(빌드에서는 원본 그대로, docs/COMBAT_ARCHITECTURE.md 8-7).
        // 스킬 표와 해금 조건 표는 진행도의 규칙(학습 조건, 배울 스킬 있음, 해금)이 읽는다.
        GameDatabaseManager database = GameDatabaseManager.Instance;
        var session = new GameSession(DataTuning.Prepare(statData), upgradeConfig, database.GetAllSkills(), database.ContentUnlocks);
        session.Apply(saveData);

        // 저장된 스테이지가 데이터에 없으면 시작하지 않는다(세이브는 그대로 남는다). 캐릭터를 만들기 전에 판정한다.
        StageData FindStage(int stageNumber) => database.GetStage(stageNumber)?.data;
        int savedStage = session.CurrentStageNumber.Value;
        if (FindStage(savedStage) == null)
        {
            Debug.LogError($"[GameInitializer] 저장된 스테이지 {savedStage}가 StageDatabase에 없어 게임을 시작하지 않습니다(세이브 보존).");
            uiRoot.ShowSaveError(SaveErrorArgs.StageMissing(savedStage));
            return;
        }

        PlayerController player = playerSpawner.EnsurePlayer();
        if (player == null)
        {
            Debug.LogError("[GameInitializer] 플레이어를 생성하지 못했습니다.");
            return;
        }

        Session = session;
#if UNITY_EDITOR
        // 플레이 중에 튜닝 덮어쓰기가 바뀌면 진행도의 파생 상태(전투 능력치, 필요 경험치, 레드닷 조건, 해금)를 다시 계산한다.
        DataTuning.Changed += Session.RecomputeFromData;
#endif

        player.Initialize(Session.Stats, playerSpawner.Character);

        combatSimulation.Initialize(player, FindStage, FlowTimings.From(database.Balance));
        EncounterDirector director = combatSimulation.Director;

        gameManager.Initialize(Session, player, director);
        background.Bind(director.CurrentStageNumber);
        uiRoot.BindGame(Session, database.GetAllSkills(), player, director);

        director.Begin(Session.CurrentStageNumber.Value);

        Player = player;
        IsInitialized = true;
        Initialized?.Invoke(this);
    }

    private void OnDestroy()
    {
#if UNITY_EDITOR
        if (Session != null) DataTuning.Changed -= Session.RecomputeFromData;
#endif
    }

    private bool ValidateReferences()
    {
        bool ok = true;
        if (statData == null) { Debug.LogError("[GameInitializer] statData(PlayerStatDatabase)가 연결되지 않았습니다."); ok = false; }
        if (upgradeConfig == null) { Debug.LogError("[GameInitializer] upgradeConfig(PlayerUpgradeConfig)가 연결되지 않았습니다."); ok = false; }
        if (playerSpawner == null) { Debug.LogError("[GameInitializer] playerSpawner가 연결되지 않았습니다."); ok = false; }
        if (gameManager == null) { Debug.LogError("[GameInitializer] gameManager가 연결되지 않았습니다."); ok = false; }
        if (background == null) { Debug.LogError("[GameInitializer] background(BackgroundScroller)가 연결되지 않았습니다."); ok = false; }
        if (uiRoot == null) { Debug.LogError("[GameInitializer] uiRoot가 연결되지 않았습니다."); ok = false; }
        if (combatSimulation == null) { Debug.LogError("[GameInitializer] combatSimulation이 연결되지 않았습니다."); ok = false; }
        else if (!combatSimulation.ValidateReferences()) ok = false;

        // BalanceConfig(흐름 시간, 성장 곡선)가 없으면 흐름을 만들 수 없다. 여기서 막아 원인을 바로 보이게 한다.
        GameDatabaseManager database = GameDatabaseManager.Instance;
        if (database == null) { Debug.LogError("[GameInitializer] 씬에 GameDatabaseManager가 없습니다."); ok = false; }
        else
        {
            if (!database.HasBalanceConfig) { Debug.LogError("[GameInitializer] GameDatabaseManager에 balanceConfig(BalanceConfig)가 연결되지 않았습니다."); ok = false; }
            if (database.ContentUnlocks == null) { Debug.LogError("[GameInitializer] GameDatabaseManager에 contentUnlockDatabase(ContentUnlockDatabase)가 연결되지 않았습니다."); ok = false; }
        }

        return ok;
    }
}
