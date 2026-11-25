using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private StageData stage;

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
        StageEntry nextStageEntry = GameDatabaseManager.Instance.GetStage(stageNumber);
        if (nextStageEntry == null || nextStageEntry.data == null)
        {
            return false;
        }

        stage = nextStageEntry.data;

        Initialize();
        GlobalGameEvents.TriggerStageChanged(stageNumber);
        return true;
    }

    private void HandleBossKilled()
    {
        if (stage.ClearType == StageClearType.KillBoss && !isStageCleared)
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
        if (encounterProgress < CurrentStage.EncountersToComplete)
        {
            return false;
        }

        encounterProgress = 0;
        bossAvailable = true;
        GlobalGameEvents.TriggerBossChallengeAvailable();
        return true;
    }

    // 다음 스테이지로 넘어가는 것은 GameManager.RestartAt(흐름 입구 하나)이 정한다.
}
