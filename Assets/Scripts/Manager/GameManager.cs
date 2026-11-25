using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private Coroutine currentSpawnLoop;
    private bool isTransitioning;

    private PlayerController player;
    private GameSession session;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private StageManager stageManager;

    [SerializeField] private float postEncounterDelay = 0.5f;

    // 전투 루프(인카운터 반복, 보스전)는 화면 프레임이 아니라 고정 단계 단위로 기다린다(docs/COMBAT_DESIGN.md 3-2).
    // 화면 프레임 단위면 스폰 시점이 기기마다 달라져 전투 결과도 달라진다. 스테이지 전환 페이드는 연출이라 화면 시간 그대로 둔다.
    private static readonly WaitForFixedUpdate WaitForStep = new WaitForFixedUpdate();

    private static int StepsFor(float seconds) => Mathf.RoundToInt(seconds / Time.fixedDeltaTime);

    // 진행도(골드 포함)의 주인은 GameSession이다. UI가 아직 GameManager를 거쳐 접근하므로
    // 1차 UI 이관(Presenter 주입) 전까지만 통로로 남겨 둔다.
    public GameSession Session => session;
    public GoldWallet Wallet => session?.Wallet;

    private void Awake()
    {
        Instance = this;
        GlobalGameEvents.OnStageCleared += StartStageTransition;
        GlobalGameEvents.OnPlayerDied += HandlePlayerDied;
        GlobalCombatEvents.OnMonsterDied += HandleMonsterDied;
        GlobalGameEvents.OnStageChanged += HandleStageChanged;
    }

    // GameInitializer가 세션 생성과 캐릭터 스폰을 끝낸 뒤 호출한다.
    // (예전에는 Start()에서 직접 플레이어를 찾고, 캐릭터가 Awake에서 읽어 둔 세이브 데이터를 꺼내 썼다.)
    public void Initialize(GameSession session, PlayerController player)
    {
        this.session = session;
        this.player = player;

        if (!ValidateReferences())
        {
            return;
        }

        // 저장 로직
        session.Stats.OnUpgraded += SaveGame;
        session.Stats.OnSkillLearned += SaveGame;
        session.Stats.OnSkillEquipChanged += SaveGame;

        if (!stageManager.Initialize(session.CurrentStageNumber))
        {
            Debug.LogError($"[GameManager] 저장된 스테이지 {session.CurrentStageNumber}를 불러오지 못했습니다.");
            return;
        }

        // 1. 게임 시작! 플레이어는 '달리는'(이동) 단계로 시작한다(PlayerController.Initialize에서 스폰된 상태).

        // 게임이 시작됐다는 사실만 알린다. HUD 표시는 각 UI가 이 이벤트를 구독해서 결정한다.
        GlobalGameEvents.TriggerGameStarted();

        // 2. 몬스터 연속 소환 루프 시작
        currentSpawnLoop = StartCoroutine(ContinuousSpawnLoop());
    }

    private void OnDestroy()
    {
        if (currentSpawnLoop != null)
        {
            StopCoroutine(currentSpawnLoop);
        }

        GlobalGameEvents.OnStageCleared -= StartStageTransition;
        GlobalGameEvents.OnPlayerDied -= HandlePlayerDied;
        GlobalCombatEvents.OnMonsterDied -= HandleMonsterDied;
        GlobalGameEvents.OnStageChanged -= HandleStageChanged;

        if (session != null)
        {
            session.Stats.OnUpgraded -= SaveGame;
            session.Stats.OnSkillLearned -= SaveGame;
            session.Stats.OnSkillEquipChanged -= SaveGame;
        }
    }

    private void HandleMonsterDied(Monster monster, GameNumber goldReward, Vector3 position)
    {
        session?.Wallet.AddGold(goldReward);
        player?.AddExp(monster.ExpReward);
    }

    // 스테이지가 바뀔 때마다 세션의 진행도에 반영해 둔다. 저장은 SaveGame()이 세션에서 한 번에 꺼낸다.
    private void HandleStageChanged(int stageNumber)
    {
        session?.SetCurrentStageNumber(stageNumber);
    }

    // 스테이지 클리어: 다음 스테이지로. 마지막 스테이지면 그 스테이지를 다시 한다.
    private void StartStageTransition()
    {
        int current = stageManager.CurrentStage.StageNumber;
        bool hasNext = GameDatabaseManager.Instance.GetStage(current + 1)?.data != null;
        if (!hasNext) Debug.Log("모든 스테이지 클리어! 마지막 스테이지를 다시 시작합니다.");

        RestartAt(hasNext ? current + 1 : current);
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

    // 사망: 지금 스테이지를 처음부터.
    private void HandlePlayerDied() => RestartAt(stageManager.CurrentStage.StageNumber);

    private IEnumerator ContinuousSpawnLoop()
    {
        while (true)
        {
            // 스폰도 고정 단계 끝에서 한다(게임 시작, 스테이지 전환 직후 포함).
            yield return WaitForStep;
            monsterSpawner.SpawnEncounter(stageManager.CurrentStage);

            while (!monsterSpawner.AllDie())
            {
                yield return WaitForStep;
            }

            for (int step = StepsFor(postEncounterDelay); step > 0; step--)
            {
                yield return WaitForStep;
            }
            monsterSpawner.ClearEncounter();

            // 보스 도전 가능 여부는 StageManager가 판단해서 이벤트로 알린다.
            stageManager.RecordEncounterCompleted();
        }
    }

    private bool ValidateReferences()
    {
        if (session == null)
        {
            Debug.LogError("GameManager needs a GameSession.");
            return false;
        }

        if (player == null)
        {
            Debug.LogError("GameManager needs a PlayerController reference.");
            return false;
        }

        if (monsterSpawner == null)
        {
            Debug.LogError("GameManager needs a MonsterSpawner reference.");
            return false;
        }

        if (stageManager == null)
        {
            Debug.LogError("GameManager needs a StageManager reference.");
            return false;
        }

        return true;
    }

    public PlayerController GetPlayer() => player;

    public void EnterBossBattle()
    {
        if (currentSpawnLoop != null)
        {
            StopCoroutine(currentSpawnLoop);
        }

        currentSpawnLoop = StartCoroutine(BossBattleRoutine());
    }

    private IEnumerator BossBattleRoutine()
    {
        Debug.Log("보스전 시작!!!");

        player.FullHeal();
        monsterSpawner.ClearEncounter();

        for (int step = StepsFor(1.0f); step > 0; step--)
        {
            yield return WaitForStep;
        }

        monsterSpawner.SpawnBoss(stageManager.CurrentStage);

        while (!monsterSpawner.AllDie() && player.IsAlive)
        {
            yield return WaitForStep;
        }
    }

    // ── 흐름 입구 (docs/DEV_TOOLS.md T4) ──────────────────────
    // 사망, 클리어, 진행도 교체, 개발 도구의 스테이지 이동이 모두 이 하나로 "스테이지를 처음부터" 시작한다.
    // 4단계에서 흐름을 EncounterDirector로 옮길 때 이 입구를 옮긴다.

    public bool IsTransitioning => isTransitioning;

    // 페이드 → 인카운터 정리 → 스테이지 초기화 → 부활 → 저장 → 스폰 루프. 전환 중이거나 없는 스테이지면 false.
    public bool RestartAt(int stageNumber)
    {
        if (isTransitioning)
        {
            Debug.LogWarning($"[GameManager] 스테이지 전환 중이라 스테이지 {stageNumber} 시작 요청을 무시합니다.");
            return false;
        }

        if (GameDatabaseManager.Instance.GetStage(stageNumber)?.data == null)
        {
            Debug.LogError($"[GameManager] 스테이지 {stageNumber}가 StageDatabase에 없습니다.");
            return false;
        }

        if (currentSpawnLoop != null) StopCoroutine(currentSpawnLoop);
        currentSpawnLoop = null;
        StartCoroutine(TransitionRoutine(stageNumber));
        return true;
    }

    private IEnumerator TransitionRoutine(int stageNumber)
    {
        isTransitioning = true;
        yield return StartCoroutine(FadeController.Instance.FadeTransition(0.5f, () => EnterStage(stageNumber)));
        isTransitioning = false;

        currentSpawnLoop = StartCoroutine(ContinuousSpawnLoop());
    }

    private void EnterStage(int stageNumber)
    {
        monsterSpawner.ClearEncounter();

        StageData previous = stageManager.CurrentStage;
        if (previous != null && previous.StageNumber == stageNumber)
        {
            // 같은 스테이지를 처음부터. 스테이지가 바뀐 것이 아니라서 알리지 않는다(스테이지 안내가 뜨지 않음).
            stageManager.Initialize();
        }
        else
        {
            if (previous != null) ClearStagePools(previous);
            stageManager.Initialize(stageNumber);   // 스테이지 변경 알림 → 세션의 스테이지 번호, 배경, 안내
        }

        player.Revive();
        SaveGame();
    }

    // 스테이지를 떠날 때 그 스테이지 몬스터의 풀을 비운다.
    private static void ClearStagePools(StageData stage)
    {
        foreach (int monsterId in new[] { stage.NormalMonsterId, stage.BossMonsterId })
        {
            MonsterEntry entry = GameDatabaseManager.Instance.GetMonster(monsterId);
            if (entry?.prefab != null) PoolManager.Instance.ClearPool(entry.prefab);
        }
    }

    // ── 진행도 교체 (docs/DEV_TOOLS.md T5) ────────────────────
    // 진행도를 통째로 바꾸고 그 진행도의 스테이지를 처음부터 시작한다(저장 포함).
    // 저장할 때와 같은 검사를 통과한 값만 받는다. 개발 도구의 새로 시작·백업 복원·레벨 지정이 쓰고,
    // 서버가 진행도를 되돌릴 때(docs/SERVER_DESIGN.md 2장)도 이 길을 쓴다.
    public bool LoadSnapshot(PlayerSaveData snapshot, out string error)
    {
        if (session == null) { error = "게임이 시작되지 않았습니다(세션 없음)."; return false; }
        if (isTransitioning) { error = "스테이지 전환 중입니다. 끝난 뒤 다시 하세요."; return false; }
        if (snapshot == null) { error = "스냅샷이 없습니다."; return false; }

        // 저장 형식(JSON)으로 한 번 거친다: 검사는 JSON을 보고, 세션은 넘겨받은 객체와 데이터를 공유하지 않는다.
        string json = JsonUtility.ToJson(snapshot);
        if (!SaveDataValidator.TryValidate(json, out error)) return false;

        PlayerSaveData copy = JsonUtility.FromJson<PlayerSaveData>(json);
        if (GameDatabaseManager.Instance.GetStage(copy.currentStageNumber)?.data == null)
        {
            error = $"스테이지 {copy.currentStageNumber}가 StageDatabase에 없습니다.";
            return false;
        }

        int maxLevel = session.Stats.MaxLevel;
        if (copy.stats.level > maxLevel)
        {
            error = $"레벨 {copy.stats.level}이 최대 레벨 {maxLevel}보다 큽니다.";
            return false;
        }

        session.Apply(copy);
        return RestartAt(session.CurrentStageNumber);
    }
}
