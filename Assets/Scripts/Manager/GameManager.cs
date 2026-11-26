using System;
using System.Collections;
using UnityEngine;

// 진행도와 시뮬레이션 사이의 일: 저장, 처치 보상, 진행도 교체.
// 흐름(인카운터, 보스, 스테이지 전환)은 여기가 아니라 EncounterDirector(고정 단계 ①)가 갖는다(docs/COMBAT_ARCHITECTURE.md 10절).
// 예전에는 흐름 코루틴 셋과 StageManager, 정적 사건 사슬이 여기에 모여 있었다.
public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private PlayerController player;
    private GameSession session;
    private EncounterDirector director;
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();

    // 진행도(골드 포함)의 주인은 GameSession이다. UI가 아직 GameManager를 거쳐 접근하므로
    // 1차 UI 이관(Presenter 주입) 전까지만 통로로 남겨 둔다.
    public GameSession Session => session;
    public GoldWallet Wallet => session?.Wallet;

    private void Awake()
    {
        Instance = this;
    }

    // GameInitializer가 세션, 캐릭터, 시뮬레이션(월드, 디렉터)을 만든 뒤, 흐름을 시작하기(Begin) 전에 호출한다.
    public void Initialize(GameSession session, PlayerController player, CombatWorld world, EncounterDirector director)
    {
        this.session = session ?? throw new ArgumentNullException(nameof(session));
        this.player = player != null ? player : throw new ArgumentNullException(nameof(player));
        this.director = director ?? throw new ArgumentNullException(nameof(director));
        if (world == null) throw new ArgumentNullException(nameof(world));

        // 처치 보상: 사망 사건이 스폰 때 정한 보상을 싣고 온다(5-A). 몬스터 뷰는 보상을 모른다.
        world.Events.Subscribe(HandleCombatEvent).AddTo(subscriptions);

        // 저장 계기: 레벨업, 업그레이드, 스킬 학습·장착(플레이어가 직접 한 명령), 스테이지 입장(게임 시작 제외)
        session.Stats.LevelReached.Subscribe(_ => SaveGame()).AddTo(subscriptions);
        session.Stats.Upgraded.Subscribe(_ => SaveGame()).AddTo(subscriptions);
        session.Stats.SkillLearned.Subscribe(_ => SaveGame()).AddTo(subscriptions);
        session.Stats.SkillEquipChanged.Subscribe(_ => SaveGame()).AddTo(subscriptions);
        director.StageEntered.Subscribe(HandleStageEntered).AddTo(subscriptions);
    }

    private void OnDestroy() => subscriptions.Dispose();

    private void HandleCombatEvent(CombatEvent e)
    {
        if (e.Type != CombatEventType.Died) return;

        if (e.Gold > GameNumber.Zero) session.Wallet.AddGold(e.Gold);
        if (e.Exp > 0) session.Stats.AddExp(e.Exp);
    }

    // 스테이지에 들어갈 때마다 세션의 스테이지 번호에 반영하고 저장한다. 게임 시작은 읽은 세이브 그대로라 저장하지 않는다.
    private void HandleStageEntered(StageEntered entered)
    {
        session.SetCurrentStageNumber(entered.StageNumber);
        if (entered.Reason == StageEntryReason.Start) return;

        Debug.Log($"[GameManager] 스테이지 {entered.StageNumber} 입장({entered.Reason})");
        SaveGame();
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

    // 진행도 전체(스탯/골드/스테이지)는 GameSession이 갖고 있으므로 거기서 한 번에 꺼내 저장한다.
    public void SaveGame()
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

    private IEnumerator BuildSaveRoutine()
    {
        if (session == null) return null;

        return SaveStorageProvider.Current.Save(session.ToSaveData());
    }

    public PlayerController GetPlayer() => player;

    // ── 진행도 교체 (docs/DEV_TOOLS.md T5) ────────────────────
    // 진행도를 통째로 바꾸고 그 진행도의 스테이지를 처음부터 시작한다(전환 가운데에서 저장).
    // 저장할 때와 같은 검사를 통과한 값만 받는다. 개발 도구의 새로 시작·백업 복원·레벨 지정이 쓰고,
    // 서버가 진행도를 되돌릴 때(docs/SERVER_DESIGN.md 2장)도 이 길을 쓴다.
    public bool LoadSnapshot(PlayerSaveData snapshot, out string error)
    {
        if (session == null || director == null) { error = "게임이 시작되지 않았습니다(세션 없음)."; return false; }
        if (snapshot == null) { error = "스냅샷이 없습니다."; return false; }

        // 저장 형식(JSON)으로 한 번 거친다: 검사는 JSON을 보고, 세션은 넘겨받은 객체와 데이터를 공유하지 않는다.
        string json = JsonUtility.ToJson(snapshot);
        if (!SaveDataValidator.TryValidate(json, out error)) return false;

        PlayerSaveData copy = JsonUtility.FromJson<PlayerSaveData>(json);
        int maxLevel = session.Stats.MaxLevel;
        if (copy.stats.level > maxLevel)
        {
            error = $"레벨 {copy.stats.level}이 최대 레벨 {maxLevel}보다 큽니다.";
            return false;
        }

        // 재시작을 먼저 받아 둔다(실행은 다음 고정 단계). 받지 못하면 진행도를 바꾸지 않는다.
        switch (director.RestartAt(copy.currentStageNumber))
        {
            case RestartResult.Accepted:
                session.Apply(copy);
                return true;
            case RestartResult.NoSuchStage:
                error = $"스테이지 {copy.currentStageNumber}가 StageDatabase에 없습니다.";
                return false;
            default:
                error = "스테이지 전환 중입니다. 끝난 뒤 다시 하세요.";
                return false;
        }
    }
}
