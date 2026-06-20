using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    private enum GameState
    {
        Running,
        Fighting,
        StageLoopComplete
    }

    public static GameManager Instance;

    private bool isScrolling = true;

    private GameState currentState;
    private int gold;
    private Coroutine currentSpawnLoop;

    [SerializeField] private PlayerController player;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private StageManager stageManager;

    [SerializeField] private UVScroller background;

    [SerializeField] private float postEncounterDelay = 0.5f;

    public int CurrentStageNumber => stageManager != null ? stageManager.CurrentStage.Key : 0;
    public string CurrentState => currentState.ToString();
    public int EncounterProgress => stageManager != null ? stageManager.EncounterProgress : 0;
    public int EncountersToComplete => stageManager != null ? stageManager.CurrentStage.encountersToComplete : 0;
    public int Gold => gold;

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
    }

    private void Start()
    {
        //TODO: 저장된 플레이어 스테이지번호(임시)
        int currStage = 1;
        if (!ValidateReferences() || !stageManager.Initialize(currStage))
        {
            return;
        }

        // 1. 게임 시작! 플레이어는 '달리는' 상태로 시작한다. (알아서 스크롤 켜짐)
        player.fsm.ChangeState(new PlayerRunState(player));

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
            currentState = GameState.Running;

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

    public void AddGold(int amount)
    {
        this.gold += amount;
    }

    public bool TrySpendGold(int amount)
    {
        if (amount <= 0) return true;
        if (gold < amount) return false;
        gold -= amount;
        return true;
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
        currentState = GameState.Fighting;
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
