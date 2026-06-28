using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    private bool isScrolling = true;

    private Coroutine currentSpawnLoop;

    private PlayerController player;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private StageManager stageManager;

    [SerializeField] private float postEncounterDelay = 0.5f;

    private readonly GoldWallet wallet = new GoldWallet();
    public GoldWallet Wallet => wallet;

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

    private void Start()
    {
        player = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.EnsurePlayer() : null;

        int currStage = player != null ? player.SaveData.currentStageNumber : 1;
        if (player != null)
        {
            wallet.LoadGold(player.SaveData.gold);

            // 저장 로직
            player.Stats.OnUpgraded += SaveGame;
            player.Stats.OnSkillLearned += SaveGame;
            player.Stats.OnSkillEquipChanged += SaveGame;
        }

        if (!ValidateReferences() || !stageManager.Initialize(currStage))
        {
            return;
        }

        // 1. 게임 시작! 플레이어는 '달리는' 상태로 시작한다.
        player.fsm.ChangeState(new PlayerRunState(player));

        // 플레이어 HUD(체력/경험치/골드) 표시
        UIManager.Instance.ShowUI<PlayerHud>();

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

        if (player != null)
        {
            player.Stats.OnUpgraded -= SaveGame;
            player.Stats.OnSkillLearned -= SaveGame;
            player.Stats.OnSkillEquipChanged -= SaveGame;
        }
    }

    private void HandleStageChanged(int stageNumber)
    {
        UIManager.Instance.ShowUI<StageAnnouncement>(announcement => announcement.SetStageNumber(stageNumber));
    }

    private void HandleMonsterDied(Monster monster, int goldReward, Vector3 position)
    {
        wallet.AddGold(goldReward);
        player?.AddExp(monster.ExpReward);
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

    // 골드/스테이지 진행도를 PlayerSaveData에 합쳐서 저장. 
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
        if (player == null) return null;

        PlayerSaveData saveData = player.SaveData;
        saveData.gold = wallet.Gold;
        saveData.currentStageNumber = stageManager.CurrentStage.StageNumber;
        return SaveStorageProvider.Current.Save(saveData);
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

            if (stageManager.RecordEncounterCompleted())
            {
                UIManager.Instance.ShowUI<BossChallengeButton>();
            }
        }
    }

    private bool ValidateReferences()
    {
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
