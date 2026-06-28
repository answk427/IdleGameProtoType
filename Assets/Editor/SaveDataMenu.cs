using UnityEditor;
using UnityEngine;

public static class SaveDataMenu
{
    [MenuItem("IdleGame/Reset Save Data")]
    private static void ResetSaveData()
    {
        SaveStorageProvider.Current.Delete();
        Debug.Log("[SaveDataMenu] 세이브 데이터 초기화 완료 — Play 모드를 다시 시작하면 반영됩니다.");
    }
}
