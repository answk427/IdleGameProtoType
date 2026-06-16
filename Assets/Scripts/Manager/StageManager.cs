using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private StageData stage;

    private int currentStageIndex;
    private int encounterProgress;
    private bool bossAvailable;
    private bool isStageCleared;

    public StageData CurrentStage => stage;
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
        if(!DataManager.Instance.StageDict.TryGetValue(stageNumber, out StageData nextStage))
        {
            return false;
        }

        stage = nextStage;
        currentStageIndex = stageNumber;

        Initialize();
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

        GlobalGameEvents.TriggerStageChanged(stage);
        return true;
    }

    public bool IsCompleted()
    {
        return isStageCleared;
    }
}
