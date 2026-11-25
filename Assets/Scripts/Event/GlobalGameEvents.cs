using System;

public static class GlobalGameEvents
{
    public static event Action OnBossKilled;
    public static event Action OnStageCleared;
    public static event Action<int> OnStageChanged;
    public static event Action OnPlayerDied;
    public static event Action OnGameStarted;
    public static event Action OnBossChallengeAvailable;

    public static void TriggerBossKilled() => OnBossKilled?.Invoke();
    public static void TriggerStageCleared() => OnStageCleared?.Invoke();
    public static void TriggerStageChanged(int stageNumber) => OnStageChanged?.Invoke(stageNumber);
    public static void TriggerPlayerDied() => OnPlayerDied?.Invoke();
    public static void TriggerGameStarted() => OnGameStarted?.Invoke();
    public static void TriggerBossChallengeAvailable() => OnBossChallengeAvailable?.Invoke();
}
