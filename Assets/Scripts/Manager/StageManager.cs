using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private StageData stage;

    private Texture2D currentBackgroundTexture;
    private int currentStageIndex;
    private int encounterProgress;
    private bool bossAvailable;
    private bool isStageCleared;

    public StageData CurrentStage => stage;
    public Texture2D CurrentBackgroundTexture => currentBackgroundTexture;
    public int EncounterProgress => encounterProgress;
    public bool BossAvailable => bossAvailable;

    private void OnEnable()
    {
        GlobalGameEvents.OnBossKilled += HandleBossKilled;
    }

    private void OnDisable()
    {
        GlobalGameEvents.OnBossKilled -= HandleBossKilled;
    }


    public void Initialize()
    {
        encounterProgress = 0;
        bossAvailable = false;
        isStageCleared = false;
    }

    public bool Initialize(int stageNumber)
    {
        StageEntry nextStageEntry = GameDatabaseManager.Instance.GetStage(stageNumber);
        if (nextStageEntry == null || nextStageEntry.data == null)
        {
            return false;
        }

        stage = nextStageEntry.data;
        currentBackgroundTexture = nextStageEntry.backgroundTexture;
        currentStageIndex = stageNumber;

        Initialize();
        // 최초 진입(GameManager.Start)과 전환(TryAdvanceStage) 양쪽 모두 여기를
        // 거치므로, 배경 갱신 트리거를 한 곳으로 모은다.
        GlobalGameEvents.TriggerStageChanged(currentBackgroundTexture);
        return true;
    }

    private void HandleBossKilled()
    {
        if (stage.clearType == StageClearType.KillBoss && !isStageCleared)
        {
            CompleteStage();
        }
    }

    private void CompleteStage()
    {
        isStageCleared = true;
        Debug.Log($"<color=yellow>[스테이지 클리어 판정!]</color>");

        GlobalGameEvents.TriggerStageCleared();
    }

    public bool RecordEncounterCompleted()
    {
        encounterProgress++;
        if (encounterProgress < CurrentStage.encountersToComplete)
        {
            return false;
        }

        encounterProgress = 0;
        bossAvailable = true;
        return true;
    }

    public bool TryAdvanceStage()
    {
        if (!IsCompleted())
        {
            return false;
        }

        if (!Initialize(currentStageIndex + 1))
        {
            return false;
        }

        return true;
    }

    public bool IsCompleted()
    {
        return isStageCleared;
    }
}
