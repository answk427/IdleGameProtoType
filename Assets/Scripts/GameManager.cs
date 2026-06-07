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
    
    // 세상이 스크롤 중인지(플레이어가 달리고 있는지) 알려주는 스위치
    private bool isScrolling = true;
    public event Action<bool> OnScrollStateChanged;

    private GameState currentState;
    private int gold;
    private Coroutine gameLoop;

    [SerializeField] private PlayerController player;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private StageManager stageManager;
    [SerializeField] private float runTimeBeforeEncounter = 2f;
    [SerializeField] private float postEncounterDelay = 0.5f;

    public int CurrentStageNumber => stageManager != null ? stageManager.CurrentStage.StageNumber : 0;
    public string CurrentState => currentState.ToString();
    public int EncounterProgress => stageManager != null ? stageManager.EncounterProgress : 0;
    public int EncountersToComplete => stageManager != null ? stageManager.CurrentStage.EncountersToComplete : 0;
    public int Gold => gold;

    public bool IsScrolling
    {
        get => isScrolling;
        set
        {
            if (isScrolling == value) return;
            isScrolling = value;

            OnScrollStateChanged.Invoke(isScrolling);
        }
    }

    private void Awake()
    {
        Instance = this;
    }


    private void Start()
    {
        if (!ValidateReferences())
        {
            return;
        }

        if (!stageManager.Initialize())
        {
            return;
        }

        gameLoop = StartCoroutine(GameLoop());
    }

    private void OnDestroy()
    {
        if (gameLoop != null)
        {
            StopCoroutine(gameLoop);
        }
    }

    private IEnumerator GameLoop()
    {
        while (true)
        {
            yield return RunUntilEncounter();
            yield return FightEncounter(stageManager.CurrentStage);
        }
    }

    private IEnumerator RunUntilEncounter()
    {
        currentState = GameState.Running;

        player.fsm.ChangeState(new PlayerRunState(player));
        
        yield return new WaitForSeconds(runTimeBeforeEncounter);
    }

    private IEnumerator FightEncounter(StageConfig stage)
    {
        currentState = GameState.Fighting;

        List<Monster> monsters = monsterSpawner.SpawnEncounter(stage);
        monsters.Sort((left, right) => left.transform.position.x.CompareTo(right.transform.position.x));

        while (!monsterSpawner.AllDie())
        {
            yield return null;
        }
        //foreach (Monster monster in monsters)
        //{
        //    yield return FightMonster(monster);
        //}

        yield return new WaitForSeconds(postEncounterDelay);
        monsterSpawner.ClearEncounter();

        if (stageManager.RecordEncounterCompleted())
        {
            currentState = GameState.StageLoopComplete;
            Debug.Log($"Stage {stage.StageNumber} loop complete. Boss is available.");
            yield return new WaitForSeconds(postEncounterDelay);
        }
    }

    private IEnumerator FightMonster(Monster monster)
    {
        player.fsm.ChangeState(new PlayerAttackState(player, monster));

        while (monster != null && monster.isActiveAndEnabled && monster.IsAlive)
        {
            yield return null;
        }

        if (monster != null)
        {
            gold += monster.GoldReward;
            Debug.Log($"Gold +{monster.GoldReward}. Total Gold: {gold}");
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

    public Monster GetClosestMonster(float originX)
    {
        if (monsterSpawner == null) return null;
        return monsterSpawner.GetClosestMonster(originX);
    }
}
