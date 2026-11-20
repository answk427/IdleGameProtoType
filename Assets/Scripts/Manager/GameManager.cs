using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private bool isScrolling = true;

    private Coroutine currentSpawnLoop;

    private PlayerController player;
    private GameSession session;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private StageManager stageManager;

    [SerializeField] private float postEncounterDelay = 0.5f;

    // 진행도(골드 포함)의 주인은 GameSession이다. UI가 아직 GameManager를 거쳐 접근하므로
    // 1차 UI 이관(Presenter 주입) 전까지만 통로로 남겨 둔다.
    public GameSession Session => session;
    public GoldWallet Wallet => session?.Wallet;

    public bool IsScrolling
    {
        get => isScrolling;
        set
        {
            if (isScrolling == value) return;
            isScrolling = value;
            GlobalGameEvents.TriggerScrollChanged(isScrolling);
        }
    }

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

        // 1. 게임 시작! 플레이어는 '달리는' 상태로 시작한다.
        player.fsm.ChangeState(new PlayerRunState(player));

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

    private void StartStageTransition()
    {
        if (currentSpawnLoop != null) StopCoroutine(currentSpawnLoop);
        StartCoroutine(TransitionToStage(true));
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

    private void HandlePlayerDied()
    {
        if (currentSpawnLoop != null) StopCoroutine(currentSpawnLoop);
        StartCoroutine(TransitionToStage(false));
    }

    private IEnumerator ContinuousSpawnLoop()
    {
        while (true)
        {
            monsterSpawner.SpawnEncounter(stageManager.CurrentStage);

            while (!monsterSpawner.AllDie())
            {
                yield return null;
            }

            yield return new WaitForSeconds(postEncounterDelay);
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

    public Monster GetClosestMonster(float originX)
    {
        if (monsterSpawner == null) return null;
        return monsterSpawner.GetClosestMonster(originX);
    }

    public List<Monster> GetMonstersInRange(float originX, float radius)
    {
        if (monsterSpawner == null) return new List<Monster>();
        return monsterSpawner.GetMonstersInRange(originX, radius);
    }

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

        yield return new WaitForSeconds(1.0f);

        monsterSpawner.SpawnBoss(stageManager.CurrentStage);

        while (!monsterSpawner.AllDie() && player.IsAlive)
        {
            yield return null;
        }
    }

    private IEnumerator TransitionToStage(bool canNext)
    {
        yield return StartCoroutine(FadeController.Instance.FadeTransition(0.5f, () =>
        {
            TransitionToStageInternal(canNext);
        }));

        currentSpawnLoop = StartCoroutine(ContinuousSpawnLoop());
    }

    private void TransitionToStageInternal(bool canNext)
    {
        monsterSpawner.ClearEncounter();

        if (canNext)
        {
            int monsterId = stageManager.CurrentStage.NormalMonsterId;
            int bossId = stageManager.CurrentStage.BossMonsterId;

            MonsterEntry monsterEntry = GameDatabaseManager.Instance.GetMonster(monsterId);
            if (monsterEntry?.prefab != null)
            {
                PoolManager.Instance.ClearPool(monsterEntry.prefab);
            }

            MonsterEntry bossEntry = GameDatabaseManager.Instance.GetMonster(bossId);
            if (bossEntry?.prefab != null)
            {
                PoolManager.Instance.ClearPool(bossEntry.prefab);
            }

            if (stageManager.TryAdvanceStage())
            {
                // TryAdvanceStage 내부에서 currentStageIndex가 +1 됨.
            }
            else
            {
                Debug.Log("모든 스테이지 클리어! 재시작!");
                stageManager.Initialize();
            }
        }

        player.Revive();

        if (!canNext)
        {
            stageManager.Initialize();
        }

        SaveGame();
    }
}
