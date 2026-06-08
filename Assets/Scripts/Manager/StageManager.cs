using UnityEngine;

public class StageManager : MonoBehaviour
{
    [SerializeField] private StageConfig[] stages;

    private int currentStageIndex;
    private int encounterProgress;
    private bool bossAvailable;

    public StageConfig CurrentStage => stages[currentStageIndex];
    public int EncounterProgress => encounterProgress;
    public bool BossAvailable => bossAvailable;

    public bool Initialize()
    {
        if (stages == null || stages.Length == 0)
        {
            Debug.LogError("StageManager needs at least one StageConfig.");
            return false;
        }

        currentStageIndex = Mathf.Clamp(currentStageIndex, 0, stages.Length - 1);
        encounterProgress = 0;
        bossAvailable = false;
        return true;
    }

    public bool RecordEncounterCompleted()
    {
        encounterProgress++;
        Debug.Log($"currentProgress:{encounterProgress}, completeProgress:{CurrentStage.EncountersToComplete}");
        if (encounterProgress < CurrentStage.EncountersToComplete)
        {
            return false;
        }

        encounterProgress = 0;
        bossAvailable = true;
        return true;
    }

    public bool TryAdvanceStage()
    {
        if (!bossAvailable || currentStageIndex >= stages.Length - 1)
        {
            return false;
        }

        currentStageIndex++;
        encounterProgress = 0;
        bossAvailable = false;
        return true;
    }
}
