using UnityEditor;
using UnityEngine;

public static class SaveDataMenu
{
    private const string ResetMenuPath = "IdleGame/Reset Save Data";
    private const string DefaultProfileMenuPath = "IdleGame/Save Profile/Default (내 진행도)";
    private const string VerificationProfileMenuPath = "IdleGame/Save Profile/Verification (claude)";

    // 지우는 대상은 지금 켜진 프로필의 세이브
    [MenuItem(ResetMenuPath)]
    private static void ResetSaveData()
    {
        SaveStorageProvider.Current.Delete();
        Debug.Log("[SaveDataMenu] 세이브 데이터 초기화 완료 — 다음 Play부터 새로 시작합니다.");
    }

    // 플레이 중에 지우면 종료 저장(OnApplicationQuit)이 현재 세션으로 파일을 되살리므로 막는다.
    [MenuItem(ResetMenuPath, true)]
    private static bool CanResetSaveData() => !EditorApplication.isPlayingOrWillChangePlaymode;

    [MenuItem(DefaultProfileMenuPath, priority = 100)]
    private static void UseDefaultProfile() => SaveProfile.SetActive(null);

    [MenuItem(DefaultProfileMenuPath, true)]
    private static bool CanUseDefaultProfile()
    {
        Menu.SetChecked(DefaultProfileMenuPath, SaveProfile.IsDefault);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }

    [MenuItem(VerificationProfileMenuPath, priority = 101)]
    private static void UseVerificationProfile() => SaveProfile.SetActive(SaveProfile.VerificationProfile);

    [MenuItem(VerificationProfileMenuPath, true)]
    private static bool CanUseVerificationProfile()
    {
        Menu.SetChecked(VerificationProfileMenuPath, SaveProfile.ActiveName == SaveProfile.VerificationProfile);
        return !EditorApplication.isPlayingOrWillChangePlaymode;
    }
}
