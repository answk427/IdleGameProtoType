using System;

public static class GlobalGameEvents
{
    public static event Action OnBossKilled;
    public static event Action OnStageCleared;
    // 스테이지 번호가 바뀌었다는 사실 하나만 전역으로 알린다. 배경 텍스처/안내 UI 등
    // 파생 정보는 구독자가 각자 GameDatabaseManager로 조회해서 쓴다 (구독자별 전용
    // 이벤트를 따로 만들면 파생 정보가 늘어날 때마다 이벤트가 같이 늘어나는 문제가 있었음).
    public static event Action<int> OnStageChanged;
    public static event Action<bool> OnScrollChanged;
    public static event Action OnPlayerDied;

    public static void TriggerBossKilled() => OnBossKilled?.Invoke();
    public static void TriggerStageCleared() => OnStageCleared?.Invoke();
    public static void TriggerStageChanged(int stageNumber) => OnStageChanged?.Invoke(stageNumber);
    public static void TriggerScrollChanged(bool isScrolling) => OnScrollChanged?.Invoke(isScrolling);
    public static void TriggerPlayerDied() => OnPlayerDied?.Invoke();
}
