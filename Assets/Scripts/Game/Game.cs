using System;
using UnityEngine;

// 게임 한 판(docs/COMBAT_ARCHITECTURE.md 12절 D45). 순수 C#이고 씬을 모른다.
// 진행도(GameSession) + 전투(월드, 흐름, 루프, 적 풀) + 그 둘을 잇는 규칙을 갖는다:
//   - 처치 보상: 사망 사건이 싣고 온 보상(스폰 때 정함)을 골드·경험치로 준다
//   - 능력치·장착 반영(PlayerCombat), 스킬 명령
//   - 저장 계기(레벨업, 업그레이드, 스킬 학습·장착, 스테이지 입장)와 진행도 교체(LoadSnapshot)
// 저장 I/O는 하지 않는다. 저장할 때가 되면 SaveRequested를 내고, 앱(GameRunner)이 저장소에 쓴다.
// 게임(GameInitializer + GameRunner)과 씬 없는 실행(테스트, 이후 오프라인 보상)이 같은 객체를 쓴다.
// 바깥(뷰, UI, 앱, 개발자 도구)에는 읽기 전용 접점(월드, 흐름)과 명령만 낸다. 전투를 만들고 진행하는 객체(월드, 디렉터, 루프, 시계,
// 적 풀, 플레이어 전투원)는 이 안에만 있다(13-6, D52).
public sealed class Game : IDisposable
{
    private readonly CompositeDisposable subscriptions = new CompositeDisposable();
    private readonly Subject<SaveReason> saveRequested = new Subject<SaveReason>();

    // 데이터와 세이브로 게임을 만든다. 세이브는 바꾸지 않는다. 시작할 수 없으면(저장된 스테이지가 데이터에 없음 등) 게임 없이 이유를 준다.
    public static GameStart TryCreate(GameData data, PlayerSaveData save, in GameOptions options)
    {
        if (data == null) throw new ArgumentNullException(nameof(data));

        var session = new GameSession(data.PlayerStats, data.Upgrades, data.SkillRows, data.Unlocks);
        session.Apply(save);

        int savedStage = session.CurrentStageNumber.Value;
        if (data.Stage(savedStage) == null) return GameStart.Failed(GameStartError.StageMissing, savedStage);

        if (!data.TryGetPlayer(options.CharacterId, session.Stats.CombatStats.Value, out CombatantSpec player))
            return GameStart.Failed(GameStartError.CharacterMissing, savedStage);

        return GameStart.Started(new Game(data, session, player, options));
    }

    private readonly CombatWorld world;
    private readonly EncounterDirector director;
    private readonly SimulationLoop loop;

    private Game(GameData data, GameSession session, in CombatantSpec player, in GameOptions options)
    {
        Data = data;
        Session = session;
        world = new CombatWorld(new SimClock(options.StepLength));

        // 플레이어가 먼저 등록된다(같은 팀 안의 처리 순서). 몬스터는 디렉터가 스폰할 때 등록한다.
        Player = new PlayerCombat(session.Stats, data, player);
        Player.Track(world);
        world.Add(Player.Combatant);

        Roster = new EnemyRoster(data);
        director = new EncounterDirector(world, Roster, data.Stage, data.FlowTimings);
        loop = new SimulationLoop(world, director, options.MaxFrameSeconds);

        // 처치 보상: 사망 사건이 스폰 때 정한 보상을 싣고 온다(5-A). 뷰는 보상을 모른다.
        world.Events.Subscribe(HandleCombatEvent).AddTo(subscriptions);

        // 저장 계기: 플레이어가 직접 한 명령, 스테이지 입장(게임 시작 제외)
        session.Stats.LevelReached.Subscribe(_ => saveRequested.OnNext(SaveReason.LevelUp)).AddTo(subscriptions);
        session.Stats.Upgraded.Subscribe(_ => saveRequested.OnNext(SaveReason.Upgrade)).AddTo(subscriptions);
        session.Stats.SkillLearned.Subscribe(_ => saveRequested.OnNext(SaveReason.SkillLearned)).AddTo(subscriptions);
        session.Stats.SkillEquipChanged.Subscribe(_ => saveRequested.OnNext(SaveReason.SkillEquipped)).AddTo(subscriptions);
        director.StageEntered.Subscribe(HandleStageEntered).AddTo(subscriptions);
    }

    public GameData Data { get; }
    public GameSession Session { get; }
    // 플레이어의 HP 상태, 쿨다운 비율, 스킬 요청(UI의 입구)
    public PlayerCombat Player { get; }

    // 전투의 읽기 전용 접점: 사건, 전투원(읽기), 앞장, 원점 이동 합, 시각
    public IReadOnlyCombatWorld World => world;
    // 흐름의 접점: 상태, 사건, 명령(보스 도전, 재시작)
    public IEncounterFlow Flow => director;
    // 화면 보간 비율(0~1): 직전 두 고정 단계 사이 어디를 그릴지
    public float RenderAlpha => loop.Alpha;

    // 적 풀(테스트가 장부를 본다)
    internal EnemyRoster Roster { get; }

    // 저장할 때가 됐다. 앱이 CreateSaveData()를 저장소에 쓴다.
    public Observable<SaveReason> SaveRequested => saveRequested;

    // 흐름을 시작한다(저장된 스테이지부터). 모든 구독(뷰, UI)을 이은 뒤에 부른다: 처음 값이 반응형으로 전달된다.
    public void Begin() => director.Begin(Session.CurrentStageNumber.Value);

    // 화면 프레임 하나에 흐른 게임 시간(초)만큼 진행한다. 돌린 고정 단계 수를 돌려준다.
    public int Advance(double frameSeconds) => loop.Advance(frameSeconds);

    public PlayerSaveData CreateSaveData() => Session.ToSaveData();

    // 에디터의 튜닝 덮어쓰기로 데이터가 바뀌면: 시뮬레이션 입력(사양)을 다시 만들고, 진행도의 파생 상태를 다시 계산하고
    // (플레이어 능력치는 구독으로 따라온다), 플레이어의 장착 스킬을 새 값으로 바꿔 끼운다. 몬스터는 다음 스폰, 스테이지는 다음 입장(13-4).
    public void RecomputeFromData()
    {
        Data.RefreshSpecs();
        Session.RecomputeFromData();
        Player.RefreshFromData();
    }

    // ── 진행도 교체 (docs/DEV_TOOLS.md T5) ──
    // 진행도를 통째로 바꾸고 그 진행도의 스테이지를 처음부터 시작한다(전환 가운데에서 저장).
    // 저장할 때와 같은 검사를 통과한 값만 받는다. 개발 도구의 새로 시작·백업 복원·레벨 지정이 쓰고,
    // 서버가 진행도를 되돌릴 때(docs/SERVER_DESIGN.md 2장)도 이 길을 쓴다.
    public bool LoadSnapshot(PlayerSaveData snapshot, out string error)
    {
        if (snapshot == null) { error = "스냅샷이 없습니다."; return false; }

        // 저장 형식(JSON)으로 한 번 거친다: 검사는 JSON을 보고, 세션은 넘겨받은 객체와 데이터를 공유하지 않는다.
        string json = JsonUtility.ToJson(snapshot);
        if (!SaveDataValidator.TryValidate(json, out error)) return false;

        PlayerSaveData copy = JsonUtility.FromJson<PlayerSaveData>(json);
        int maxLevel = Session.Stats.MaxLevel;
        if (copy.stats.level > maxLevel)
        {
            error = $"레벨 {copy.stats.level}이 최대 레벨 {maxLevel}보다 큽니다.";
            return false;
        }

        // 재시작을 먼저 받아 둔다(실행은 다음 고정 단계). 받지 못하면 진행도를 바꾸지 않는다.
        switch (director.RestartAt(copy.currentStageNumber))
        {
            case RestartResult.Accepted:
                Session.Apply(copy);
                return true;
            case RestartResult.NoSuchStage:
                error = $"스테이지 {copy.currentStageNumber}가 StageDatabase에 없습니다.";
                return false;
            default:
                error = "스테이지 전환 중입니다. 끝난 뒤 다시 하세요.";
                return false;
        }
    }

    public void Dispose()
    {
        subscriptions.Dispose();
        Player.Dispose();
    }

    private void HandleCombatEvent(CombatEvent e)
    {
        if (e.Type != CombatEventType.Died) return;

        if (e.Gold > GameNumber.Zero) Session.Wallet.AddGold(e.Gold);
        if (e.Exp > 0) Session.Stats.AddExp(e.Exp);
    }

    // 스테이지에 들어갈 때마다 세션의 스테이지 번호에 반영하고 저장한다. 게임 시작은 읽은 세이브 그대로라 저장하지 않는다.
    private void HandleStageEntered(StageEntered entered)
    {
        Session.SetCurrentStageNumber(entered.StageNumber);
        if (entered.Reason != StageEntryReason.Start) saveRequested.OnNext(SaveReason.StageEntered);
    }
}

public enum SaveReason
{
    LevelUp,
    Upgrade,
    SkillLearned,
    SkillEquipped,
    StageEntered,
}

// 게임을 만들 때 앱이 정하는 것. 씬 없는 실행은 직접 넣는다.
public readonly struct GameOptions
{
    public readonly int CharacterId;          // 캐릭터 표의 id(직업 선택이 생기면 세이브나 UI가 정한다)
    public readonly double StepLength;        // 고정 단계 길이(초), SimClock.StepLengthFromSetting
    public readonly double MaxFrameSeconds;   // 한 화면 프레임에 진행하는 게임 시간의 상한(초)

    public GameOptions(int characterId, double stepLength, double maxFrameSeconds = SimulationLoop.DefaultMaxFrameSeconds)
    {
        CharacterId = characterId;
        StepLength = stepLength;
        MaxFrameSeconds = maxFrameSeconds;
    }
}

public enum GameStartError
{
    None,
    StageMissing,     // 저장된 스테이지가 스테이지 표에 없다(세이브를 지키려고 시작하지 않는다)
    CharacterMissing, // 캐릭터 표에 그 캐릭터가 없거나 행동 시간표가 연결되지 않았다(개발 오류)
}

public readonly struct GameStart
{
    public readonly Game Game;
    public readonly GameStartError Error;
    public readonly int SavedStageNumber;

    private GameStart(Game game, GameStartError error, int savedStageNumber)
    {
        Game = game;
        Error = error;
        SavedStageNumber = savedStageNumber;
    }

    public bool Succeeded => Game != null;

    public static GameStart Started(Game game) => new GameStart(game, GameStartError.None, game.Session.CurrentStageNumber.Value);
    public static GameStart Failed(GameStartError error, int savedStageNumber) => new GameStart(null, error, savedStageNumber);
}
