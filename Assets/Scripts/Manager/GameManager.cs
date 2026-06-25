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

    [SerializeField] private BackgroundScroller background;

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
    }

    private void Start()
    {
        player = PlayerSpawner.Instance != null ? PlayerSpawner.Instance.EnsurePlayer() : null;

        //TODO: 저장된 플레이어 스테이지번호(임시)
        int currStage = 1;
        if (!ValidateReferences() || !stageManager.Initialize(currStage))
        {
            return;
        }

        // 1. 게임 시작! 플레이어는 '달리는' 상태로 시작한다. (알아서 스크롤 켜짐)
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
    }

    // 몬스터 처치 보상(골드/경험치) 지급. Monster는 죽었다는 사실만 이벤트로 알리고,
    // 보상을 어떻게 지급할지는 모른다.
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
            int monsterId = stageManager.CurrentStage.normalMonsterId;
            int bossId = stageManager.CurrentStage.bossMonsterId;

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
    }
}
