using UnityEditor;
using UnityEngine;

public static class SaveDataMenu
{
    private const string ResetMenuPath = "IdleGame/Reset Save Data";

    [MenuItem(ResetMenuPath)]
    private static void ResetSaveData()
    {
        SaveStorageProvider.Current.Delete();
        Debug.Log("[SaveDataMenu] 세이브 데이터 초기화 완료 — 다음 Play부터 새로 시작합니다.");
    }

    // 플레이 중에 지우면 종료 저장(OnApplicationQuit)이 현재 세션으로 파일을 되살리므로 막는다.
    [MenuItem(ResetMenuPath, true)]
    private static bool CanResetSaveData() => !EditorApplication.isPlayingOrWillChangePlaymode;
}
