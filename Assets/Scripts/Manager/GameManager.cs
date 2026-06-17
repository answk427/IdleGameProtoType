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
    private Coroutine currentSpawnLoop;

    [SerializeField] private PlayerController player;
    [SerializeField] private MonsterSpawner monsterSpawner;
    [SerializeField] private StageManager stageManager;

    [SerializeField] private UVScroller background;

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
        if (!ValidateReferences() || !stageManager.Initialize())
        {
            return;
        }

        // 1. 게임 시작! 플레이어에게 '달리기' 본능을 주입함. (알아서 스크롤 켜짐)
        player.fsm.ChangeState(new PlayerRunState(player));

        // 2. 무한 스폰 루프 가동
        currentSpawnLoop = StartCoroutine(ContinuousSpawnLoop());
    }

    private void OnDestroy()
    {
        if (currentSpawnLoop != null)
        {
            StopCoroutine(currentSpawnLoop);
        }
    }

    private IEnumerator ContinuousSpawnLoop()
    {
        while (true)
        {
            currentState = GameState.Running;

            // 1. 화면 오른쪽(안 보이는 곳)에 N마리 스폰!
            // 플레이어는 이미 RunState이므로, 스폰되자마자 배경과 함께 몬스터가 왼쪽으로 다가옴
            monsterSpawner.SpawnEncounter(stageManager.CurrentStage);

            // 2. 스폰된 몬스터들이 전부 죽을 때까지 기다림
            while (!monsterSpawner.AllDie())
            {
                yield return null; // 플레이어 FSM이 알아서 싸우고 죽이고 다 할 거임!
            }

            // 3. 웨이브 클리어! 시체 지우기 및 스테이지 진행도 기록
            yield return new WaitForSeconds(postEncounterDelay);
            monsterSpawner.ClearEncounter();

            if (stageManager.RecordEncounterCompleted())
            {
                // 보스전 진입 UI 띄움
                UIManager.Instance.ShowUI<BossChallengeButton>();
                Debug.Log($"Stage {CurrentStageNumber} loop complete. Boss is available.");
            }

            // 루프가 다시 돌면서 즉시 다음 N마리가 스폰됨! 무한 반복!
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

    public void AddGold(int amount)
    {
        gold += amount;
    }

    public void EnterBossBattle()
    {
        // 1. 돌아가고 있던 일반 몬스터 무한 스폰 루프를 강제로 멈춤
        if (currentSpawnLoop != null)
        {
            StopCoroutine(currentSpawnLoop);
        }

        // 2. 보스전 코루틴 시작!
        currentSpawnLoop = StartCoroutine(BossBattleRoutine());
    }

    private IEnumerator BossBattleRoutine()
    {
        currentState = GameState.Fighting;
        Debug.Log("보스전 시작!!!");

        // 1. 현재 필드에 있던 일반 몬스터들 싹 다 삭제 및 화면 밖으로 치움
        monsterSpawner.ClearEncounter();

        // (선택) 보스 등장 연출을 위해 1초 정도 대기
        yield return new WaitForSeconds(1.0f);

        // 2. 보스 소환!
        monsterSpawner.SpawnBoss(stageManager.CurrentStage);

        // 3. 보스가 살아있고 && 플레이어도 살아있을 때만 대기하면서 전투
        while (!monsterSpawner.AllDie() && player.IsAlive)
        {
            yield return null;
        }

        Debug.Log("보스전 종료!");
        yield return new WaitForSeconds(postEncounterDelay);

        // 4. 다음 스테이지로 이동
        // [승리] 플레이어가 살았다는 건 보스를 잡았다는 뜻!
        if (player.IsAlive)
        {
            Debug.Log("보스 처치 성공! 다음 스테이지로!");

            yield return StartCoroutine(TransitionToStage(true));
        }
        // [패배] 플레이어가 죽었음...
        else
        {
            Debug.Log("보스전 실패... 현재 스테이지 일반 몹부터 재도전!");
            yield return StartCoroutine(TransitionToStage(false));
        }
    }

    private IEnumerator TransitionToStage(bool canNext)
    {
        yield return StartCoroutine(FadeController.Instance.FadeTransition(0.5f, () =>
        {
            TransitionToStageInternal(canNext);
        }));


        // 일반 몹 사냥 무한 루프 재가동!
        currentSpawnLoop = StartCoroutine(ContinuousSpawnLoop());
    }
    private void TransitionToStageInternal(bool canNext)
    {
        // 1. 필드에 남아있는 몬스터 디스폰
        monsterSpawner.ClearEncounter();

        if (canNext)
        {
            // 2. 이전 스테이지 몬스터 Pool 비우기
            if (stageManager.CurrentStage.MonsterPrefab != null)
            {
                PoolManager.Instance.ClearPool(stageManager.CurrentStage.MonsterPrefab);
            }
            if (stageManager.CurrentStage.BossPrefab != null)
            {
                PoolManager.Instance.ClearPool(stageManager.CurrentStage.BossPrefab);
            }

            if (stageManager.TryAdvanceStage())
            {
                // TryAdvanceStage 안에서 currentStageIndex가 +1 됨.
                // 따라서 TransitionToStage를 부르면 자연스럽게 '다음 스테이지'가 세팅됨!

            }
            else
            {
                Debug.Log("모든 스테이지 클리어! 엔딩!");
                // 마지막 스테이지 무한 반복
            }
        }

        // 3. 배경 텍스처 교체
        StageConfig currentStage = stageManager.CurrentStage;
        if (background != null)
        {
            background.ChangeTexture(currentStage.StageBackgroundTexture);
        }

        // todo : 4. 플레이어 초기화 (죽었을 수도 있으니 체력을 풀로 채우고 살림)
        //player.Revive(); // (주의: PlayerController에 체력 채우고 IsAlive=true 하는 함수 필요!)
        //player.transform.position = defaultPlayerPosition; // 위치도 처음 자리로 리셋

        // 5. 진행도(보스 게이지) 리셋
        stageManager.Initialize();

        // --- 세팅 끝! ---
    }
}
