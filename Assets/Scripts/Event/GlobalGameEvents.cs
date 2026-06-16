using System;
using UnityEngine;

public static class GlobalGameEvents
{
    public static event Action OnBossKilled;
    public static event Action OnStageCleared;
    public static event Action<StageData> OnStageChanged;
    public static event Action<bool> OnScrollChanged;
    public static event Action OnPlayerDied;

    public static void TriggerBossKilled() => OnBossKilled?.Invoke();
    public static void TriggerStageCleared() => OnStageCleared?.Invoke();
    public static void TriggerStageChanged(StageData newStage) => OnStageChanged?.Invoke(newStage);
    public static void TriggerScrollChanged(bool isScrolling) => OnScrollChanged?.Invoke(isScrolling);
    public static void TriggerPlayerDied() => OnPlayerDied?.Invoke();
}
