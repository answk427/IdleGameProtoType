using System;
using UnityEngine;

public static class GlobalGameEvents
{
    public static event Action OnBossKilled;
    public static event Action OnStageCleared;
    public static event Action<Texture2D> OnStageChanged;
    public static event Action<bool> OnScrollChanged;
    public static event Action OnPlayerDied;

    public static void TriggerBossKilled() => OnBossKilled?.Invoke();
    public static void TriggerStageCleared() => OnStageCleared?.Invoke();
    public static void TriggerStageChanged(Texture2D backgroundTexture) => OnStageChanged?.Invoke(backgroundTexture);
    public static void TriggerScrollChanged(bool isScrolling) => OnScrollChanged?.Invoke(isScrolling);
    public static void TriggerPlayerDied() => OnPlayerDied?.Invoke();
}
